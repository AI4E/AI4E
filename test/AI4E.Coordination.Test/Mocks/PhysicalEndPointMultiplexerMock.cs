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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Coordination.Mocks
{
    public sealed class PhysicalEndPointMultiplexerMock<TAddress>
        : IPhysicalEndPointMultiplexer<TAddress>
    {
        private readonly Dictionary<string, MultiplexPhysicalEndPoint> _endPoints = new Dictionary<string, MultiplexPhysicalEndPoint>();

        public PhysicalEndPointMultiplexerMock(TAddress localAddress)
        {
            LocalAddress = localAddress;
        }

        public TAddress LocalAddress { get; }

        public IReadOnlyCollection<IMultiplexPhysicalEndPoint<TAddress>> PhysicalEndPoints
            => _endPoints.Values.ToList();

        public bool AddPhysicalEndPoint(IPhysicalEndPoint<TAddress> physicalEndPoint, string multiplexName)
        {
            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            if (multiplexName == null)
                throw new ArgumentNullException(nameof(multiplexName));

            return _endPoints.TryAdd(multiplexName, new MultiplexPhysicalEndPoint(multiplexName, physicalEndPoint));
        }

        public bool RemovePhysicalEndPoint(string multiplexName)
        {
            if (multiplexName == null)
                throw new ArgumentNullException(nameof(multiplexName));

            return _endPoints.Remove(multiplexName);
        }

        public IMultiplexPhysicalEndPoint<TAddress> GetPhysicalEndPoint(string multiplexName)
        {
            if (!_endPoints.TryGetValue(multiplexName, out var result))
            {
                result = null;
            }

            return result;

        }

        public void Dispose() { }

        public string AddressToString(TAddress address)
        {
            throw new NotImplementedException();
        }

        public TAddress AddressFromString(string str)
        {
            throw new NotImplementedException();
        }

        private sealed class MultiplexPhysicalEndPoint : IMultiplexPhysicalEndPoint<TAddress>
        {
            public MultiplexPhysicalEndPoint(string multiplexName, IPhysicalEndPoint<TAddress> physicalEndPoint)
            {
                MultiplexName = multiplexName;
                PhysicalEndPoint = physicalEndPoint;
            }

            public string MultiplexName { get; }
            public TAddress LocalAddress => PhysicalEndPoint.LocalAddress;
            public IPhysicalEndPoint<TAddress> PhysicalEndPoint { get; }

            public void Dispose()
            {
                PhysicalEndPoint.Dispose();
            }

            public ValueTask<Transmission<TAddress>> ReceiveAsync(CancellationToken cancellation)
            {
                return PhysicalEndPoint.ReceiveAsync(cancellation);
            }

            public ValueTask SendAsync(Transmission<TAddress> transmission, CancellationToken cancellation = default)
            {
                return PhysicalEndPoint.SendAsync(transmission, cancellation);
            }

            public string AddressToString(TAddress address)
            {
                throw new NotImplementedException();
            }

            public TAddress AddressFromString(string str)
            {
                throw new NotImplementedException();
            }
        }
    }
}
