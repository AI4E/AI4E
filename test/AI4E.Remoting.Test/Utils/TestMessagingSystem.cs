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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace AI4E.Remoting.Utils
{
    // TODO: Move this to a shared project and remove the dependency of Coordination.Test > Remoting.Test
    public sealed class TestMessagingSystem
    {
        private readonly ConcurrentDictionary<TestMessagingSystemAddress, PhysicalEndPoint> _physicalEndPoints;
        private int _nextAddress = 0;

        public TestMessagingSystem()
        {
            _physicalEndPoints = new ConcurrentDictionary<TestMessagingSystemAddress, PhysicalEndPoint>();
        }

        public IPhysicalEndPoint<TestMessagingSystemAddress> CreatePhysicalEndPoint()
        {
            var address = new TestMessagingSystemAddress(Interlocked.Increment(ref _nextAddress));
            var physicalEndPoint = new PhysicalEndPoint(address, this);

            _physicalEndPoints[address] = physicalEndPoint;

            return physicalEndPoint;
        }

        public IReadOnlyList<IPhysicalEndPoint<TestMessagingSystemAddress>> PhysicalEndPoints
            => _physicalEndPoints.Values.ToList();

        private void Deliver(
            IMessage message,
            TestMessagingSystemAddress receiverAddress,
            TestMessagingSystemAddress senderAddress)
        {
            if (_physicalEndPoints.TryGetValue(receiverAddress, out var physicalEndPoint))
            {
                physicalEndPoint.Deliver(message, senderAddress);
            }
        }

        private sealed class PhysicalEndPoint : IPhysicalEndPoint<TestMessagingSystemAddress>
        {
            private readonly AsyncProducerConsumerQueue<(IMessage message, TestMessagingSystemAddress remoteAddress)> _rxQueue;
            private readonly CancellationTokenSource _disposalSource = new CancellationTokenSource();
            private readonly TestMessagingSystem _messagingSystem;

            public PhysicalEndPoint(TestMessagingSystemAddress address, TestMessagingSystem messagingSystem)
            {
                LocalAddress = address;
                _messagingSystem = messagingSystem;
                _rxQueue = new AsyncProducerConsumerQueue<(IMessage message, TestMessagingSystemAddress remoteAddress)>();
            }

            public TestMessagingSystemAddress LocalAddress { get; }

            public async Task<(IMessage message, TestMessagingSystemAddress remoteAddress)> ReceiveAsync(CancellationToken cancellation = default)
            {
                if (_disposalSource.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                try
                {
                    using (var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_disposalSource.Token, cancellation))
                    {
                        return await _rxQueue.DequeueAsync(cancellation);
                    }
                }
                catch (OperationCanceledException) when (_disposalSource.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            public Task SendAsync(IMessage message, TestMessagingSystemAddress remoteAddress, CancellationToken cancellation = default)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (remoteAddress == default)
                    throw new ArgumentDefaultException(nameof(remoteAddress));

                if (_disposalSource.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _messagingSystem.Deliver(message, remoteAddress, LocalAddress);

                return Task.CompletedTask;
            }

            public void Deliver(IMessage message, TestMessagingSystemAddress remoteAddress)
            {
                if (_disposalSource.IsCancellationRequested)
                {
                    return;
                }

                _rxQueue.Enqueue((message, remoteAddress));
            }

            public void Dispose()
            {
                if (!_disposalSource.IsCancellationRequested)
                {
                    try
                    {
                        _disposalSource.Cancel();
                        _disposalSource.Dispose();
                    }
                    catch (ObjectDisposedException) { }
                }
            }
        }
    }

    public readonly struct TestMessagingSystemAddress : IEquatable<TestMessagingSystemAddress>
    {
        public TestMessagingSystemAddress(int rawAddress)
        {
            RawAddress = rawAddress;
        }

        public int RawAddress { get; }

        public override bool Equals(object obj)
        {
            return obj is TestMessagingSystemAddress address && Equals(address);
        }

        public bool Equals(TestMessagingSystemAddress other)
        {
            return other.RawAddress == RawAddress;
        }

        public override int GetHashCode()
        {
            return RawAddress.GetHashCode();
        }

        public static bool operator ==(TestMessagingSystemAddress left, TestMessagingSystemAddress right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TestMessagingSystemAddress left, TestMessagingSystemAddress right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class TestMessagingSystemAddressConversion
        : IAddressConversion<TestMessagingSystemAddress>
    {
        public byte[] SerializeAddress(TestMessagingSystemAddress route)
        {
            return BitConverter.GetBytes(route.RawAddress);
        }

        public TestMessagingSystemAddress DeserializeAddress(byte[] buffer)
        {
            return new TestMessagingSystemAddress(BitConverter.ToInt32(buffer, 0));
        }

        public string ToString(TestMessagingSystemAddress route)
        {
            return route.RawAddress.ToString();
        }

        public TestMessagingSystemAddress Parse(string str)
        {
            return new TestMessagingSystemAddress(int.Parse(str));
        }
    }
}
