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
