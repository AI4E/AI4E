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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace AI4E.AspNetCore.Blazor.SignalR
{
#pragma warning disable CA1812
    internal class BlazorHttpConnectionFactory : IConnectionFactory
#pragma warning restore CA1812
    {
        private readonly BlazorHttpConnectionOptions _options;
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigationManager;
        private readonly ILoggerFactory _loggerFactory;

        public BlazorHttpConnectionFactory(
            IOptions<BlazorHttpConnectionOptions> options,
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            ILoggerFactory loggerFactory)
        {
            if (jsRuntime is null)
                throw new ArgumentNullException(nameof(jsRuntime));

            if (navigationManager is null)
                throw new ArgumentNullException(nameof(navigationManager));

            _options = options.Value;
            _jsRuntime = jsRuntime;
            _navigationManager = navigationManager;
            _loggerFactory = loggerFactory;
        }

        public async ValueTask<ConnectionContext> ConnectAsync(
            EndPoint endPoint,
            CancellationToken cancellationToken = default)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            if (!(endPoint is UriEndPoint uriEndPoint))
            {
                throw new NotSupportedException(
                    $"The provided {nameof(EndPoint)} must be of type {nameof(UriEndPoint)}.");
            }

            if (_options.Url != null && _options.Url != uriEndPoint.Uri)
            {
                throw new InvalidOperationException(
                    $"If {nameof(BlazorHttpConnectionOptions)}.{nameof(BlazorHttpConnectionOptions.Url)} was set, it " +
                    $"must match the {nameof(UriEndPoint)}.{nameof(UriEndPoint.Uri)} passed to {nameof(ConnectAsync)}.");
            }

            var shallowCopiedOptions = ShallowCopyHttpConnectionOptions(_options);
            shallowCopiedOptions.Url = uriEndPoint.Uri;

            var connection = new BlazorHttpConnection(
                shallowCopiedOptions, _jsRuntime, _navigationManager, _loggerFactory);

            try
            {
                await connection.StartAsync().ConfigureAwait(false);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        // Internal for testing
        internal static BlazorHttpConnectionOptions ShallowCopyHttpConnectionOptions(
            BlazorHttpConnectionOptions options)
        {
            var result = new BlazorHttpConnectionOptions
            {
                HttpMessageHandlerFactory = options.HttpMessageHandlerFactory,
                //ClientCertificates = options.ClientCertificates,
                //Cookies = options.Cookies,
                Url = options.Url,
                Transports = options.Transports,
                SkipNegotiation = options.SkipNegotiation,
                AccessTokenProvider = options.AccessTokenProvider,
                //CloseTimeout = options.CloseTimeout,
                //Credentials = options.Credentials,
                //Proxy = options.Proxy,
                //UseDefaultCredentials = options.UseDefaultCredentials,
                DefaultTransferFormat = options.DefaultTransferFormat,
                //WebSocketConfiguration = options.WebSocketConfiguration,
            };

            result.Headers.Clear();
            foreach (var kvp in options.Headers)
                result.Headers.Add(kvp);

            return result;
        }
    }
}
