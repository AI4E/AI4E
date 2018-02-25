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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Modularity.HttpDispatch;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace AI4E.Modularity
{
    public sealed class ModuleServer : IServer
    {
        private readonly IRemoteMessageDispatcher _messageEndPoint;
        private readonly string _prefix;
        private IHandlerRegistration _handlerRegistration;

        public ModuleServer(IRemoteMessageDispatcher messageEndPoint, string prefix)
        {
            if (messageEndPoint == null)
                throw new ArgumentNullException(nameof(messageEndPoint));

            _messageEndPoint = messageEndPoint;
            _prefix = prefix;
            Features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        }

        public async Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            _handlerRegistration = _messageEndPoint.Register(new ContextualProvider<IMessageHandler<ModuleHttpRequest>>(provider => new HttpRequestForwardingHandler<TContext>(application)));

            try
            {
                await _messageEndPoint.RegisterHttpPrefixAsync(_prefix);
            }
            catch
            {
                var handlerRegistration = _handlerRegistration;
                Debug.Assert(handlerRegistration != null);

                handlerRegistration.Cancel();
                await handlerRegistration.Cancellation;

                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _messageEndPoint.UnregisterHttpPrefixAsync(_prefix);
            }
            finally
            {
                var handlerRegistration = _handlerRegistration;
                Debug.Assert(handlerRegistration != null);

                handlerRegistration.Cancel();
                await handlerRegistration.Cancellation;
            }
        }

        public IFeatureCollection Features { get; } = new FeatureCollection();

        public void Dispose() { }

        private class HttpRequestForwardingHandler<TContext> : IMessageHandler<ModuleHttpRequest>
        {
            private readonly IHttpApplication<TContext> _application;

            // TODO: What is this for?
            private static readonly MethodInfo _setMethodDefintion
                = typeof(IFeatureCollection).GetMethods().SingleOrDefault(p => p.Name == "Set" &&
                                                                               p.IsGenericMethodDefinition &&
                                                                               p.GetGenericArguments().Length == 1);

            public HttpRequestForwardingHandler(IHttpApplication<TContext> application)
            {
                Debug.Assert(application != null);
                _application = application;
            }

            public async Task<IDispatchResult> HandleAsync(ModuleHttpRequest message, DispatchValueDictionary ctx)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                var features = new FeatureCollection();

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

                var responseStream = new MemoryStream();

                var responseFeature = new HttpResponseFeature()
                {
                    Body = responseStream,
                    Headers = new HeaderDictionary()
                };

                features.Set<IHttpRequestFeature>(requestFeature);
                features.Set<IHttpResponseFeature>(responseFeature);

                var context = _application.CreateContext(features);

                try
                {
                    await _application.ProcessRequestAsync(context);

                    var httpReponse = new ModuleHttpResponse
                    {
                        StatusCode = responseFeature.StatusCode,
                        ReasonPhrase = responseFeature.ReasonPhrase,
                        Body = responseStream.ToArray(),
                        Headers = new Dictionary<string, string[]>()
                    };

                    foreach (var entry in responseFeature.Headers)
                    {
                        httpReponse.Headers.Add(entry.Key, entry.Value.ToArray());
                    }

                    var result = httpReponse;

                    _application.DisposeContext(context, null);

                    return new SuccessDispatchResult<ModuleHttpResponse>(result);
                }
                catch (Exception exc)
                {
                    _application.DisposeContext(context, exc);
                    throw;
                }
            }
        }
    }
}
