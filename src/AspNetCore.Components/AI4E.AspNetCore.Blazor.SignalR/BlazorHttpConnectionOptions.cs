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
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;

namespace AI4E.AspNetCore.Blazor.SignalR
{
    public class BlazorHttpConnectionOptions
    {
        public BlazorHttpConnectionOptions()
        {
            Headers = new Dictionary<string, string>();
            Transports = HttpTransports.All;
            Implementations = BlazorTransportType.JsWebSockets | BlazorTransportType.ManagedWebSockets |
                              BlazorTransportType.JsServerSentEvents | BlazorTransportType.ManagedServerSentEvents |
                              BlazorTransportType.ManagedLongPolling;
        }

        /// <summary>
        /// Gets or sets a delegate for wrapping or replacing the
        /// <see cref="P:Microsoft.AspNetCore.Http.Connections.Client.HttpConnectionOptions.HttpMessageHandlerFactory" />
        /// that will make HTTP requests.
        /// </summary>
        public Func<HttpMessageHandler, HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }

        /// <summary>
        /// Gets or sets a collection of headers that will be sent with HTTP requests.
        /// </summary>
        public IDictionary<string, string> Headers { get; private set; }

        /// <summary>Gets or sets the URL used to send HTTP requests.</summary>
        public Uri? Url { get; set; }

        /// <summary>
        /// Gets or sets a bitmask comprised of one or more <see cref="T:Microsoft.AspNetCore.Http.Connections.HttpTransportType" />
        /// that specify what transports the client should use to send HTTP requests.
        /// </summary>
        public HttpTransportType Transports { get; set; }

        /// <summary>
        /// Gets or sets a bitmask comprised of one or more <see cref="T:BlazorSignalR.BlazorTransportType" />
        /// that specify what transports the client should use to send HTTP requests. Used to select what implementations to use.
        /// </summary>
        public BlazorTransportType Implementations { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether negotiation is skipped when connecting to the server.
        /// </summary>
        /// <remarks>
        /// Negotiation can only be skipped when using the <see cref="F:Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets" /> transport.
        /// </remarks>
        public bool SkipNegotiation { get; set; }

        /// <summary>
        /// Gets or sets an access token provider that will be called to return a token for each HTTP request.
        /// </summary>
        public Func<Task<string?>>? AccessTokenProvider { get; set; }

        /// <summary>
        /// Gets or sets the default <see cref="TransferFormat" /> to use if <see cref="HttpConnection.StartAsync(CancellationToken)"/>
        /// is called instead of <see cref="HttpConnection.StartAsync(TransferFormat, CancellationToken)"/>.
        /// </summary>
        public TransferFormat DefaultTransferFormat { get; set; } = TransferFormat.Binary;
    }
}
