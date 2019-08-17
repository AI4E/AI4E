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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Nito.AsyncEx;

namespace AI4E.Remoting.Mocks
{
    public sealed class PhysicalEndPointMock<TAddress> : IPhysicalEndPoint<TAddress>
    {
        private bool _isDisposed;

        public PhysicalEndPointMock(TAddress localAddress)
        {
            LocalAddress = localAddress;
            RxQueue = new AsyncProducerConsumerQueue<Transmission<TAddress>>();
            TxQueue = new List<Transmission<TAddress>>();
        }

        public TAddress LocalAddress { get; }
        public AsyncProducerConsumerQueue<Transmission<TAddress>> RxQueue { get; }
        public List<Transmission<TAddress>> TxQueue { get; }

        public void Dispose()
        {
            _isDisposed = true;
        }

        public ValueTask<Transmission<TAddress>> ReceiveAsync(CancellationToken cancellation = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            return RxQueue.DequeueAsync(cancellation).AsValueTask();
        }

        public ValueTask SendAsync(Transmission<TAddress> transmission, CancellationToken cancellation = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            TxQueue.Add(transmission);

            return default;
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
