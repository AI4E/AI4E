using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using Nito.AsyncEx;

namespace AI4E.Test.Util
{
    public sealed class PhysicalMessingSubsystem
    {
        private readonly AsyncProducerConsumerQueue<IMessage> _xToY = new AsyncProducerConsumerQueue<IMessage>();
        private readonly AsyncProducerConsumerQueue<IMessage> _yToX = new AsyncProducerConsumerQueue<IMessage>();

        public PhysicalMessingSubsystem()
        {
            X = new PhysicalEndPoint(TestAddress.X, _xToY, _yToX);
            Y = new PhysicalEndPoint(TestAddress.Y, _yToX, _xToY);
        }

        public IPhysicalEndPoint<TestAddress> X { get; }

        public IPhysicalEndPoint<TestAddress> Y { get; }

        private sealed class PhysicalEndPoint : IPhysicalEndPoint<TestAddress>
        {
            private readonly AsyncProducerConsumerQueue<IMessage> _txQueue;
            private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue;

            public PhysicalEndPoint(TestAddress localAddress,
                                    AsyncProducerConsumerQueue<IMessage> txQueue,
                                    AsyncProducerConsumerQueue<IMessage> rxQueue)
            {
                if (txQueue == null)
                    throw new ArgumentNullException(nameof(txQueue));

                if (rxQueue == null)
                    throw new ArgumentNullException(nameof(rxQueue));

                LocalAddress = localAddress;
                _txQueue = txQueue;
                _rxQueue = rxQueue;
            }

            public Task SendAsync(IMessage message, TestAddress address, CancellationToken cancellation)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (address < TestAddress.X || address > TestAddress.Y)
                    throw new ArgumentException("address");

                if (address == LocalAddress)
                {
                    return _rxQueue.EnqueueAsync(message, cancellation);
                }
                else
                {
                    return _txQueue.EnqueueAsync(message, cancellation);
                }
            }

            public Task<IMessage> ReceiveAsync(CancellationToken cancellation)
            {
                return _rxQueue.DequeueAsync(cancellation);
            }

            public TestAddress LocalAddress { get; }
        }
    }
}
