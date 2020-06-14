/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * BlazorSignalR (https://github.com/csnewman/BlazorSignalR)
 *
 * MIT License
 *
 * Copyright (c) 2018 csnewman
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Net;
using AI4E.AspNetCore.Blazor.SignalR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.SignalR.Client
{
    public static class BlazorSignalRExtensions
    {
        public static IHubConnectionBuilder WithUrlBlazor(
            this IHubConnectionBuilder hubConnectionBuilder,
            string url,
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            HttpTransportType? transports = null,
            Action<BlazorHttpConnectionOptions>? options = null)
        {
            return WithUrlBlazor(hubConnectionBuilder, new Uri(url), jsRuntime, navigationManager, transports, options);
        }

        public static IHubConnectionBuilder WithUrlBlazor(
            this IHubConnectionBuilder hubConnectionBuilder,
            Uri url,
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            HttpTransportType? transports = null,
            Action<BlazorHttpConnectionOptions>? options = null)
        {
            if (hubConnectionBuilder == null)
                throw new ArgumentNullException(nameof(hubConnectionBuilder));

            if (jsRuntime == null)
                throw new ArgumentNullException(nameof(jsRuntime));

            if (navigationManager is null)
                throw new ArgumentNullException(nameof(navigationManager));

            hubConnectionBuilder.Services.Configure<BlazorHttpConnectionOptions>(o =>
            {
                o.Url = url;

                if (!transports.HasValue)
                    return;

                o.Transports = transports.Value;
            });

            if (options != null)
                hubConnectionBuilder.Services.Configure(options);

            hubConnectionBuilder.Services.AddSingleton<EndPoint, BlazorHttpConnectionOptionsDerivedHttpEndPoint>();

            hubConnectionBuilder.Services.AddSingleton<
                IConfigureOptions<BlazorHttpConnectionOptions>, BlazorHubProtocolDerivedHttpOptionsConfigurer>();

            hubConnectionBuilder.Services.AddSingleton(
                provider => BuildBlazorHttpConnectionFactory(provider, jsRuntime, navigationManager));

            return hubConnectionBuilder;
        }

#pragma warning disable CA1812
        private class BlazorHttpConnectionOptionsDerivedHttpEndPoint : UriEndPoint
#pragma warning restore CA1812
        {
            public BlazorHttpConnectionOptionsDerivedHttpEndPoint(IOptions<BlazorHttpConnectionOptions> options)
                : base(options.Value.Url)
            { }
        }
#pragma warning disable CA1812
        private class BlazorHubProtocolDerivedHttpOptionsConfigurer
            : IConfigureNamedOptions<BlazorHttpConnectionOptions>
#pragma warning restore CA1812
        {
            private readonly TransferFormat _defaultTransferFormat;

            public BlazorHubProtocolDerivedHttpOptionsConfigurer(IHubProtocol hubProtocol)
            {
                _defaultTransferFormat = hubProtocol.TransferFormat;
            }

            public void Configure(string name, BlazorHttpConnectionOptions options)
            {
                Configure(options);
            }

            public void Configure(BlazorHttpConnectionOptions options)
            {
                options.DefaultTransferFormat = _defaultTransferFormat;
            }
        }

        private static IConnectionFactory BuildBlazorHttpConnectionFactory(
            IServiceProvider provider,
            IJSRuntime jsRuntime,
            NavigationManager navigationManager)
        {
            return ActivatorUtilities.CreateInstance<BlazorHttpConnectionFactory>(
                provider,
                jsRuntime,
                navigationManager);
        }
    }
}
