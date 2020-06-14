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

using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using Microsoft.AspNetCore.Hosting.Server;

namespace AI4E.Modularity.Module
{
    [MessageHandler]
    internal sealed class ModuleHttpHandler : MessageHandler
    {
        private readonly IServer _server;

        public ModuleHttpHandler(IServer server)
        {
            _server = server;
        }

        public async ValueTask<IDispatchResult> HandleAsync(ModuleHttpRequest request, CancellationToken cancellation)
        {
            if (_server == null || !(_server is IModuleServer moduleServer))
                return DispatchFailure();

            var httpRequestExecutor = moduleServer.RequestExecutor;

            // The server is not yet started or is disposed.
            if (httpRequestExecutor == null)
                return DispatchFailure();

            var response = await httpRequestExecutor.ExecuteAsync(request, cancellation);

            // This is a special case that should be respected in order to invoke the next http module handler
            // or the next pipeline step in the senders http request pipeline.
            if (response.StatusCode == 404)
            {
                return DispatchFailure();
            }

            return Success(response);
        }
    }
}
