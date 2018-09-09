/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Coordination;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Modularity.Debug;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AI4E.Modularity.Module
{
    public sealed class ModuleServer : IServer
    {
        private readonly IRemoteMessageDispatcher _messageDispatcher;
        private readonly IMetadataAccessor _metadataAccessor;
        private readonly IRunningModuleLookup _runningModules;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ModuleServer> _logger;
        private readonly AsyncDisposeHelper _disposeHelper;

        private readonly string _prefix;
        private readonly bool _isDebuggingConnection;
        private IAsyncDisposable _handlerRegistration;

        public ModuleServer(IRemoteMessageDispatcher messageDispatcher,
                            IMetadataAccessor metadataAccessor,
                            IRunningModuleLookup runningModules,
                            IServiceProvider serviceProvider,
                            IOptions<ModuleServerOptions> optionsAccessor,
                            ILogger<ModuleServer> logger)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (metadataAccessor == null)
                throw new ArgumentNullException(nameof(metadataAccessor));

            if (runningModules == null)
                throw new ArgumentNullException(nameof(runningModules));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (string.IsNullOrWhiteSpace(options.Prefix))
            {
                throw new ArgumentException("A url prefix must be specified.");
            }

            _messageDispatcher = messageDispatcher;
            _metadataAccessor = metadataAccessor;
            _runningModules = runningModules;
            _serviceProvider = serviceProvider;
            _logger = logger;

            _disposeHelper = new AsyncDisposeHelper(DiposeInternalAsync);
            _prefix = options.Prefix;
            _isDebuggingConnection = options.UseDebugConnection;
            Features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        }

        public IFeatureCollection Features { get; } = new FeatureCollection();

        public async Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            if (_isDebuggingConnection)
            {
                var tasks = new List<Task>();

                if (_serviceProvider.GetRequiredService<ILogicalEndPoint>() is IAsyncInitialization logicalEndPoint)
                {
                    tasks.Add(logicalEndPoint.Initialization.WithCancellation(cancellationToken));
                }

                if (_serviceProvider.GetRequiredService<ICoordinationManager>() is IAsyncInitialization coordinationManager)
                {
                    tasks.Add(coordinationManager.Initialization.WithCancellation(cancellationToken));
                }

                await Task.WhenAll(tasks);
            }

            async Task DispatchDebugModuleConnected()
            {
                var endPoint = await _messageDispatcher.GetLocalEndPointAsync(cancellationToken);
                var metadata = await _metadataAccessor.GetMetadataAsync(cancellationToken);
                var debugConnection = _serviceProvider.GetRequiredService<DebugConnection>();
                var address = await debugConnection.GetLocalAddressAync(cancellationToken);
                var addressConversion = _serviceProvider.GetRequiredService<IAddressConversion<IPEndPoint>>();
                var addressBytes = addressConversion.SerializeAddress(address);
                var message = new DebugModuleConnected(addressBytes, endPoint, metadata.Module, metadata.Version);

                await _messageDispatcher.DispatchAsync(message, publish: true, cancellationToken);
            }

            async Task RegisterModuleAsync()
            {
                var endPoint = await _messageDispatcher.GetLocalEndPointAsync(cancellationToken);
                var metadata = await _metadataAccessor.GetMetadataAsync(cancellationToken);
                var module = metadata.Module;

                var moduleRegistrationOperation = _runningModules.AddModuleAsync(module, endPoint, _prefix.Yield(), cancellationToken);

                await moduleRegistrationOperation;
            }

            async Task RegisterHandlerAsync()
            {
                var handler = Provider.Create(() => new HttpRequestForwardingHandler<TContext>(application, _logger));
                _handlerRegistration = _messageDispatcher.Register(handler);

                if (_handlerRegistration is IAsyncInitialization asyncInitialization)
                {
                    await asyncInitialization.Initialization.WithCancellation(cancellationToken);
                }
            }

            await Task.WhenAll(RegisterHandlerAsync(), RegisterModuleAsync());

            if (_isDebuggingConnection)
            {
                DispatchDebugModuleConnected().HandleExceptions(_logger);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _disposeHelper.DisposeAsync().WithCancellation(cancellationToken);
        }

        #region Disposal

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        private async Task DiposeInternalAsync()
        {
            async Task UnregisterModuleAsync()
            {
                var endPoint = await _messageDispatcher.GetLocalEndPointAsync(cancellation: default);
                var metadata = await _metadataAccessor.GetMetadataAsync(cancellation: default);
                var module = metadata.Module;
                var moduleUnregistrationOperation = _runningModules.RemoveModuleAsync(module, cancellation: default);

                await moduleUnregistrationOperation;
            }

            Task UnregisterHandlerAsync()
            {
                return _handlerRegistration?.DisposeAsync() ?? Task.CompletedTask;
            }

            await Task.WhenAll(UnregisterHandlerAsync().HandleExceptionsAsync(_logger),
                               UnregisterModuleAsync().HandleExceptionsAsync(_logger));
        }

        #endregion

        private sealed class HttpRequestForwardingHandler<TContext> : IMessageHandler<ModuleHttpRequest>
        {
            private readonly IHttpApplication<TContext> _application;
            private readonly ILogger<ModuleServer> _logger;

            public HttpRequestForwardingHandler(IHttpApplication<TContext> application, ILogger<ModuleServer> logger)
            {
                System.Diagnostics.Debug.Assert(application != null);

                _application = application;
                _logger = logger;
            }

            public async ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary<ModuleHttpRequest> dispatchData,
                                                                CancellationToken cancellation)
            {
                if (dispatchData == null)
                    throw new ArgumentNullException(nameof(dispatchData));

                var message = dispatchData.Message;
                var responseStream = new MemoryStream();
                var requestFeature = BuildRequestFeature(message);
                var responseFeature = BuildResponseFeature(responseStream);
                var features = BuildFeatureCollection(requestFeature, responseFeature);
                var context = _application.CreateContext(features);

                try
                {
                    await _application.ProcessRequestAsync(context);
                    var response = BuildResponse(responseStream, responseFeature);

                    DisposeContext(context);

                    return new SuccessDispatchResult<ModuleHttpResponse>(response);
                }
                catch (Exception exc)
                {
                    DisposeContext(context, exc);
                    throw;
                }
            }

            private void DisposeContext(TContext context, Exception exc = null)
            {
                try
                {
                    _application.DisposeContext(context, exc);
                }
                catch (Exception exc2)
                {
                    _logger?.LogWarning(exc2, "Unable to dispose context.");
                }
            }

            private static ModuleHttpResponse BuildResponse(MemoryStream responseStream, HttpResponseFeature responseFeature)
            {
                var response = new ModuleHttpResponse
                {
                    StatusCode = responseFeature.StatusCode,
                    ReasonPhrase = responseFeature.ReasonPhrase,
                    Body = responseStream.ToArray(),
                    Headers = new Dictionary<string, string[]>()
                };

                foreach (var entry in responseFeature.Headers)
                {
                    response.Headers.Add(entry.Key, entry.Value.ToArray());
                }

                return response;
            }

            private static FeatureCollection BuildFeatureCollection(HttpRequestFeature requestFeature, HttpResponseFeature responseFeature)
            {
                var features = new FeatureCollection();
                features.Set<IHttpRequestFeature>(requestFeature);
                features.Set<IHttpResponseFeature>(responseFeature);
                return features;
            }

            private HttpResponseFeature BuildResponseFeature(MemoryStream responseStream)
            {
                return new HttpResponseFeature() { Body = responseStream, Headers = new HeaderDictionary() };
            }

            private static HttpRequestFeature BuildRequestFeature(ModuleHttpRequest message)
            {
                var requestFeature = new HttpRequestFeature
                {
                    Method = message.Method,
                    Path = message.Path,
                    PathBase = message.PathBase,
                    Protocol = message.Protocol,
                    QueryString = message.QueryString,
                    RawTarget = message.RawTarget,
                    Scheme = message.Scheme,
                    Body = new MemoryStream(message.Body),
                    Headers = new HeaderDictionary()
                };

                foreach (var header in message.Headers)
                {
                    requestFeature.Headers.Add(header.Key, new StringValues(header.Value));
                }

                return requestFeature;
            }
        }
    }
}
