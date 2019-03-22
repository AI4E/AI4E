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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using Nito.AsyncEx;

namespace AI4E.Coordination.Utils
{
    public sealed class InMemoryPhysicalEndPoint : IPhysicalEndPoint<InMemoryPhysicalAddress>
    {
        private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();

        public InMemoryPhysicalAddress LocalAddress => InMemoryPhysicalAddress.Instance;

        public async Task<(IMessage message, InMemoryPhysicalAddress remoteAddress)> ReceiveAsync(CancellationToken cancellation)
        {
            return (await _rxQueue.DequeueAsync(cancellation), InMemoryPhysicalAddress.Instance);
        }

        public Task SendAsync(IMessage message, InMemoryPhysicalAddress remoteAddress, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (remoteAddress == null)
                throw new ArgumentNullException(nameof(remoteAddress));

            return _rxQueue.EnqueueAsync(message, cancellation);
        }

        public void Dispose() { }
    }

    public sealed class InMemoryPhysicalAddress
    {
        private InMemoryPhysicalAddress() { }

        public static InMemoryPhysicalAddress Instance { get; } = new InMemoryPhysicalAddress();
    }

    public sealed class InMemoryPhysicalAddressConversion : IAddressConversion<InMemoryPhysicalAddress>
    {
        public byte[] SerializeAddress(InMemoryPhysicalAddress route)
        {
            if (route == null)
                return null;

            return new byte[] { 123 };
        }

        public InMemoryPhysicalAddress DeserializeAddress(byte[] buffer)
        {
            if (buffer == null)
                return null;

            if (buffer.Length != 1 || buffer[0] != 123)
                throw new ArgumentException();

            return InMemoryPhysicalAddress.Instance;
        }

        public string ToString(InMemoryPhysicalAddress route)
        {
            if (route == null)
                return null;

            return "x";
        }

        public InMemoryPhysicalAddress Parse(string str)
        {
            if (str == null)
                return null;

            if (str != "x")
                throw new ArgumentException();

            return InMemoryPhysicalAddress.Instance;
        }
    }
}
