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

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Blazor.SignalR
{
    internal class BlazorAccessTokenHttpMessageHandler : DelegatingHandler
    {
        private readonly BlazorHttpConnection _httpConnection;

        public BlazorAccessTokenHttpMessageHandler(
            HttpMessageHandler inner,
            BlazorHttpConnection httpConnection)
            : base(inner)
        {
            _httpConnection = httpConnection;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessTokenAsync = await _httpConnection.GetAccessTokenAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(accessTokenAsync))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessTokenAsync);

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
