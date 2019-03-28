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
using Nito.AsyncEx;

namespace AI4E.Remoting.Test.Mocks
{
    public sealed class PhysicalEndPointMock<TAddress> : IPhysicalEndPoint<TAddress>
    {
        private bool _isDisposed;

        public PhysicalEndPointMock(TAddress localAddress)
        {
            LocalAddress = localAddress;
            RxQueue = new AsyncProducerConsumerQueue<(IMessage message, TAddress remoteAddress)>();
            TxQueue = new List<(IMessage message, TAddress remoteAddress)>();
        }

        public TAddress LocalAddress { get; }
        public AsyncProducerConsumerQueue<(IMessage message, TAddress remoteAddress)> RxQueue { get; }
        public List<(IMessage message, TAddress remoteAddress)> TxQueue { get; }

        public Task<(IMessage message, TAddress remoteAddress)> ReceiveAsync(CancellationToken cancellation)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            return RxQueue.DequeueAsync(cancellation);
        }

        public Task SendAsync(IMessage message, TAddress remoteAddress, CancellationToken cancellation)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            TxQueue.Add((message, remoteAddress));

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
