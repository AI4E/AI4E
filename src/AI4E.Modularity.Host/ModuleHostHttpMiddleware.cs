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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Routing;
using AI4E.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    internal sealed class ModuleHostHttpMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IRemoteMessageDispatcher _dispatcher;
        private readonly IPathMapper _pathMapper;

        public ModuleHostHttpMiddleware(RequestDelegate next, IRemoteMessageDispatcher dispatcher, IPathMapper pathMapper)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (pathMapper == null)
                throw new ArgumentNullException(nameof(pathMapper));

            _next = next;
            _dispatcher = dispatcher;
            _pathMapper = pathMapper;
        }

        public async Task Invoke(HttpContext context)
        {
            Assert(context != null);

            if (context != null)
            {
                var cancellation = context.RequestAborted;
                EndPointAddress endPoint;

                try
                {
                    endPoint = await MapPathAsync(context, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    return;
                }

                if (endPoint != EndPointAddress.UnknownAddress)
                {
                    var requestMessage = await PackRequestMessage(context);

                    IDispatchResult dispatchResult;

                    try
                    {
                        dispatchResult = await DispatchToEndPointAsync(endPoint, requestMessage, cancellation);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    if (dispatchResult.IsSuccessWithResult<ModuleHttpResponse>(out var responseMessage))
                    {
                        var responseFeature = await UnpackResponseMessage(context, responseMessage);

                        if (responseFeature.StatusCode != 404)
                        {
                            return;
                        }
                    }
                }
            }
            await _next?.Invoke(context);
        }

        private async Task<EndPointAddress> MapPathAsync(HttpContext context, CancellationToken cancellation)
        {
            return await _pathMapper.MapHttpPathAsync(context.Features.Get<IHttpRequestFeature>().Path.AsMemory(), cancellation);
        }

        private async Task<IDispatchResult> DispatchToEndPointAsync(EndPointAddress endPoint, ModuleHttpRequest moduleHttpRequest, CancellationToken cancellation)
        {
            return await _dispatcher.DispatchAsync(new DispatchDataDictionary<ModuleHttpRequest>(moduleHttpRequest), publish: false, endPoint, cancellation);
        }

        private static IHttpRequestFeature GetRequestFeature(HttpContext context)
        {
            return context.Features.FirstOrDefault(p => p.Key == typeof(IHttpRequestFeature)).Value as IHttpRequestFeature;
        }

        private static IHttpResponseFeature GetResponseFeature(HttpContext context)
        {
            return context.Features.FirstOrDefault(p => p.Key == typeof(IHttpResponseFeature)).Value as IHttpResponseFeature;
        }

        private static async Task<ModuleHttpRequest> PackRequestMessage(HttpContext context)
        {
            var requestFeature = GetRequestFeature(context);

            var result = new ModuleHttpRequest
            {
                Method = requestFeature.Method,
                Path = requestFeature.Path,
                PathBase = requestFeature.PathBase,
                Protocol = requestFeature.Protocol,
                QueryString = requestFeature.QueryString,
                RawTarget = requestFeature.RawTarget,
                Scheme = requestFeature.Scheme,
                Body = requestFeature.Body == null ? Array.Empty<byte>() : await requestFeature.Body.ToArrayAsync(),
                Headers = new Dictionary<string, string[]>()
            };

            foreach (var entry in requestFeature.Headers)
            {
                result.Headers.Add(entry.Key, entry.Value.ToArray());
            }

            return result;
        }

        private static async Task<IHttpResponseFeature> UnpackResponseMessage(HttpContext context, ModuleHttpResponse responseMessage)
        {
            var responseFeature = GetResponseFeature(context);

            responseFeature.StatusCode = responseMessage.StatusCode;
            responseFeature.ReasonPhrase = responseMessage.ReasonPhrase;

            responseFeature.Headers.Clear();

            foreach (var header in responseMessage.Headers)
            {
                responseFeature.Headers.Add(header.Key, new StringValues(header.Value));
            }

            if (responseMessage.Body.Length > 0)
            {
                await responseFeature.Body.WriteAsync(responseMessage.Body, 0, responseMessage.Body.Length);
            }

            return responseFeature;
        }
    }
}
