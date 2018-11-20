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
using System.Linq;
using AI4E.Internal;
using AI4E.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace AI4E.Modularity.Host
{
    public static class ApplicationBuilderExtension
    {
        private static readonly byte[] _emptyArray = new byte[0];

        public static void UseModularity(this IApplicationBuilder applicationBuilder)
        {
            if (applicationBuilder == null)
                throw new ArgumentNullException(nameof(applicationBuilder));

            var serviceProvider = applicationBuilder.ApplicationServices;

            if (serviceProvider.GetService<ModularityMarkerService>() == null)
            {
                throw new InvalidOperationException("Cannot use the modular host without adding the modularity services.");
            }

            // Initialize the module-host.
            var dispatcher = serviceProvider.GetRequiredService<IRemoteMessageDispatcher>();
            var runningModuleLookup = serviceProvider.GetRequiredService<IRunningModuleLookup>();

            applicationBuilder.Use(async (context, next) =>
            {
                var cancellation = context?.RequestAborted ?? default;
                var endPoint = await runningModuleLookup.MapHttpPathAsync(context.Features.Get<IHttpRequestFeature>().Path, cancellation);

                if (endPoint != EndPointAddress.UnknownAddress)
                {
                    var requestFeature = context.Features.FirstOrDefault(p => p.Key == typeof(IHttpRequestFeature)).Value as IHttpRequestFeature;

                    var moduleHttpRequest = new ModuleHttpRequest
                    {
                        Method = requestFeature.Method,
                        Path = requestFeature.Path,
                        PathBase = requestFeature.PathBase,
                        Protocol = requestFeature.Protocol,
                        QueryString = requestFeature.QueryString,
                        RawTarget = requestFeature.RawTarget,
                        Scheme = requestFeature.Scheme,
                        Body = requestFeature.Body == null ? _emptyArray : await requestFeature.Body.ToArrayAsync(),
                        Headers = new Dictionary<string, string[]>()
                    };

                    foreach (var entry in requestFeature.Headers)
                    {
                        moduleHttpRequest.Headers.Add(entry.Key, entry.Value.ToArray());
                    }

                    var message = moduleHttpRequest;
                    var dispatchResult = await dispatcher.DispatchAsync(new DispatchDataDictionary<ModuleHttpRequest>(message), publish: false, endPoint, cancellation);
                    var response = default(ModuleHttpResponse);

                    if (dispatchResult.IsSuccess)
                    {
                        response = (dispatchResult as IDispatchResult<ModuleHttpResponse>).Result;
                    }

                    if (response == null)
                    {
                        await next?.Invoke();
                        return;
                    }

                    var responseFeature = context.Features.FirstOrDefault(p => p.Key == typeof(IHttpResponseFeature)).Value as IHttpResponseFeature;

                    responseFeature.StatusCode = response.StatusCode;
                    responseFeature.ReasonPhrase = response.ReasonPhrase;

                    responseFeature.Headers.Clear();

                    foreach (var header in response.Headers)
                    {
                        responseFeature.Headers.Add(header.Key, new StringValues(header.Value));
                    }

                    if (response.Body.Length > 0)
                    {
                        await responseFeature.Body.WriteAsync(response.Body, 0, response.Body.Length);
                    }

                    if(responseFeature.StatusCode == 404)
                    {
                        await next?.Invoke();
                        return;
                    }
                }
                else
                {
                    await next?.Invoke();
                }
            });
        }
    }
}
