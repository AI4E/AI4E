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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Modularity.HttpDispatch;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AI4E.Modularity
{
    public sealed class ModuleServer : IServer
    {
        private readonly IRemoteMessageDispatcher _messageEndPoint;
        private readonly string _prefix;
        private readonly EndPointRoute _localEndPoint;
        private IHandlerRegistration _handlerRegistration;

        public ModuleServer(IRemoteMessageDispatcher messageEndPoint, IOptions<ModuleServerOptions> optionsAccessor)
        {
            if (messageEndPoint == null)
                throw new ArgumentNullException(nameof(messageEndPoint));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (string.IsNullOrWhiteSpace(options.Prefix))
            {
                throw new ArgumentException("A url prefix must be specified.");
            }

            _messageEndPoint = messageEndPoint;
            _prefix = options.Prefix;
            Features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        }

        public async Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            var handler = Provider.Create(() => new HttpRequestForwardingHandler<TContext>(application));

            _handlerRegistration = _messageEndPoint.Register(handler);

            if (!await RegisterHttpPrefixAsync())
            {
                try
                {
                    var handlerRegistration = _handlerRegistration;
                    Debug.Assert(handlerRegistration != null);

                    handlerRegistration.Cancel();
                    await handlerRegistration.Cancellation;
                }
                finally
                {
                    throw new ModuleServerException("Failed to start server.");
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var result = await UnregisterHttpPrefixAsync();

            try
            {
                var handlerRegistration = _handlerRegistration;
                Debug.Assert(handlerRegistration != null);

                handlerRegistration.Cancel();
                await handlerRegistration.Cancellation;
            }
            finally
            {
                throw new ModuleServerException("Failed to stop server.");
            }
        }

        private async Task<bool> RegisterHttpPrefixAsync()
        {
            var message = new RegisterHttpPrefix(_messageEndPoint.LocalEndPoint, _prefix);
            var result = await _messageEndPoint.DispatchAsync(message);

            if (!result.IsSuccess)
            {
                // TODO: Log result message
            }

            return result.IsSuccess;
        }

        private async Task<bool> UnregisterHttpPrefixAsync()
        {
            var message = new UnregisterHttpPrefix(_prefix);
            var result = await _messageEndPoint.DispatchAsync(message);

            if (!result.IsSuccess)
            {
                // TODO: Log result message
            }

            return result.IsSuccess;
        }

        public IFeatureCollection Features { get; } = new FeatureCollection();

        public void Dispose() { }

        private class HttpRequestForwardingHandler<TContext> : IMessageHandler<ModuleHttpRequest>
        {
            private readonly IHttpApplication<TContext> _application;

            public HttpRequestForwardingHandler(IHttpApplication<TContext> application)
            {
                Debug.Assert(application != null);
                _application = application;
            }

            public async Task<IDispatchResult> HandleAsync(ModuleHttpRequest message, DispatchValueDictionary ctx)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                var responseStream = new MemoryStream();
                var requestFeature = BuildRequestFeature(message);
                var responseFeature = BuildResponseFeature(responseStream);
                var features = BuildFeatureCollection(requestFeature, responseFeature);
                var context = _application.CreateContext(features);

                try
                {
                    await _application.ProcessRequestAsync(context);
                    var response = BuildResponse(responseStream, responseFeature);

                    try
                    {
                        _application.DisposeContext(context, null);
                    }
                    catch { /* TODO: Logging*/ }

                    return new SuccessDispatchResult<ModuleHttpResponse>(response);
                }
                catch (Exception exc)
                {
                    _application.DisposeContext(context, exc);
                    throw;
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
