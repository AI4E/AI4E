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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AI4E.AspNetCore.Blazor.SignalR
{
    internal class LoggingHttpMessageHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHttpMessageHandler>? _logger;

        public LoggingHttpMessageHandler(HttpMessageHandler inner, ILoggerFactory? loggerFactory)
            : base(inner)
        {
            if (loggerFactory == null)
                throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory?.CreateLogger<LoggingHttpMessageHandler>();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Log.SendingHttpRequest(_logger, request.Method, request.RequestUri);
            var httpResponseMessage = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                Log.UnsuccessfulHttpResponse(_logger, httpResponseMessage.StatusCode, request.Method,
                                  request.RequestUri);
            }

            return httpResponseMessage;
        }

        private static class Log
        {
            private static readonly Action<ILogger, HttpMethod, Uri, Exception?> _sendingHttpRequest =
                LoggerMessage.Define<HttpMethod, Uri>(LogLevel.Trace, new EventId(1, "SendingHttpRequest"),
                    "Sending HTTP request {RequestMethod} '{RequestUrl}'.");

            private static readonly Action<ILogger, int, HttpMethod, Uri, Exception?> _unsuccessfulHttpResponse =
                LoggerMessage.Define<int, HttpMethod, Uri>(LogLevel.Warning, new EventId(2, "UnsuccessfulHttpResponse"),
                    "Unsuccessful HTTP response {StatusCode} return from {RequestMethod} '{RequestUrl}'.");

            public static void SendingHttpRequest(ILogger? logger, HttpMethod requestMethod, Uri requestUrl)
            {
                if (logger is null)
                    return;

                Log._sendingHttpRequest(logger, requestMethod, requestUrl, null);
            }

            public static void UnsuccessfulHttpResponse(ILogger? logger, HttpStatusCode statusCode,
                HttpMethod requestMethod, Uri requestUrl)
            {
                if (logger is null)
                    return;

                Log._unsuccessfulHttpResponse(logger, (int)statusCode, requestMethod, requestUrl, null);
            }
        }
    }
}
