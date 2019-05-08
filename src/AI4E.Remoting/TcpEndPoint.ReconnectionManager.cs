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

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.Extensions.Logging;

namespace AI4E.Remoting
{
    public sealed partial class TcpEndPoint
    {
        private sealed class ReconnectionManager : ReconnectionManagerBase
        {
            private readonly RemoteEndPoint _remoteEndPoint;

            public ReconnectionManager(RemoteEndPoint remoteEndPoint, ILogger logger = null) : base(logger)
            {
                Debug.Assert(remoteEndPoint != null);

                _remoteEndPoint = remoteEndPoint;
            }

            protected override ValueTask EstablishConnectionAsync(bool isInitialConnection, CancellationToken cancellation)
            {
                return _remoteEndPoint.EstablishConnectionAsync(isInitialConnection, cancellation);
            }

            protected override ValueTask OnConnectionEstablished(CancellationToken cancellation)
            {
                return _remoteEndPoint.OnConnectionEstablished(cancellation);
            }

            protected override ValueTask OnConnectionEstablishing(CancellationToken cancellation)
            {
                return _remoteEndPoint.OnConnectionEstablishing(cancellation);
            }
        }
    }
}
