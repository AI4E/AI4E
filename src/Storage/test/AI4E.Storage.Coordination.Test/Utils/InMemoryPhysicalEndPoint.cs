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
using AI4E.Utils.Messaging.Primitives;
using Nito.AsyncEx;

namespace AI4E.Storage.Coordination.Utils
{
    public sealed class InMemoryPhysicalEndPoint : IPhysicalEndPoint<InMemoryPhysicalAddress>
    {
        private readonly AsyncProducerConsumerQueue<Message> _rxQueue = new AsyncProducerConsumerQueue<Message>();

        public InMemoryPhysicalAddress LocalAddress => InMemoryPhysicalAddress.Instance;

        public async ValueTask<Transmission<InMemoryPhysicalAddress>> ReceiveAsync(CancellationToken cancellation)
        {
            return new Transmission<InMemoryPhysicalAddress>(await _rxQueue.DequeueAsync(cancellation), InMemoryPhysicalAddress.Instance);
        }

        public ValueTask SendAsync(Transmission<InMemoryPhysicalAddress> transmission, CancellationToken cancellation)
        {
            return _rxQueue.EnqueueAsync(transmission.Message, cancellation).AsValueTask();
        }

        public string AddressToString(InMemoryPhysicalAddress address)
        {
            if (address == null)
                return null;

            return "x";
        }

        public InMemoryPhysicalAddress AddressFromString(string str)
        {
            if (str == null)
                return null;

            if (str != "x")
                throw new ArgumentException();

            return InMemoryPhysicalAddress.Instance;
        }

        public void Dispose() { }
    }

    public sealed class InMemoryPhysicalAddress
    {
        private InMemoryPhysicalAddress() { }

        public static InMemoryPhysicalAddress Instance { get; } = new InMemoryPhysicalAddress();
    }
}
