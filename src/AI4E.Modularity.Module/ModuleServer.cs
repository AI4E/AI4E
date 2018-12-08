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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Routing;
using AI4E.Utils;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace AI4E.Modularity.Module
{
    public sealed class ModuleServer : IServer
    {
        private readonly IRemoteMessageDispatcher _messageDispatcher;
        private readonly IMetadataAccessor _metadataAccessor;
        private readonly IModuleManager _moduleManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger<ModuleServer> _logger;
        private readonly AsyncDisposeHelper _disposeHelper;

        private readonly string _prefix;
        private IHandlerRegistration _handlerRegistration;

        private bool _isStarted = false;
        private readonly AsyncLock _lock = new AsyncLock();

        public ModuleServer(IRemoteMessageDispatcher messageDispatcher,
                            IMetadataAccessor metadataAccessor,
                            IModuleManager runningModules,
                            IServiceProvider serviceProvider,
                            IOptions<ModuleServerOptions> optionsAccessor,
                            ILoggerFactory loggerFactory)
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
            _moduleManager = runningModules;
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory?.CreateLogger<ModuleServer>();

            _disposeHelper = new AsyncDisposeHelper(DiposeInternalAsync, AsyncDisposeHelperOptions.Synchronize);
            _prefix = options.Prefix;
            Features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        }

        #region IServer

        public IFeatureCollection Features { get; } = new FeatureCollection();

        public async Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            using (var guard = await _disposeHelper.GuardDisposalAsync(cancellationToken))
            {
                cancellationToken = guard.Cancellation;

                using (await _lock.LockAsync(cancellationToken))
                {
                    if (_isStarted)
                    {
                        return;
                    }

                    _isStarted = true;

                    await RegisterHandlerAsync(application, cancellationToken);

                    try
                    {
                        await RegisterModuleAsync(cancellationToken);
                    }
                    catch
                    {
                        await UnregisterModuleAsync().HandleExceptionsAsync(_logger);
                        _isStarted = false;

                        throw;
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _disposeHelper.DisposeAsync();
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        private async Task DiposeInternalAsync()
        {
            await UnregisterModuleAsync().HandleExceptionsAsync(_logger);
            await UnregisterHandlerAsync().HandleExceptionsAsync(_logger);
        }

        #endregion

        private async Task RegisterHandlerAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellation)
        {
            var handler = CreateHandlerProvider(application);
            _handlerRegistration = _messageDispatcher.Register(handler);

            await _handlerRegistration.Initialization;
        }

        private IProvider<ModuleHttpHandler<TContext>> CreateHandlerProvider<TContext>(IHttpApplication<TContext> application)
        {
            return Provider.Create(() => new ModuleHttpHandler<TContext>(application, _loggerFactory.CreateLogger<ModuleHttpHandler<TContext>>()));
        }

        private Task UnregisterHandlerAsync()
        {
            return _handlerRegistration.DisposeAsync();
        }

        private async Task RegisterModuleAsync(CancellationToken cancellation)
        {
            var endPoint = await _messageDispatcher.GetLocalEndPointAsync(cancellation);
            var metadata = await _metadataAccessor.GetMetadataAsync(cancellation);
            await _moduleManager.AddModuleAsync(metadata.Module, endPoint, _prefix.AsMemory().Yield(), cancellation);
        }

        private async Task UnregisterModuleAsync()
        {
            var metadata = await _metadataAccessor.GetMetadataAsync(cancellation: default);
            await _moduleManager.RemoveModuleAsync(metadata.Module, cancellation: default);
        }
    }
}
