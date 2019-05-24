using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Utils;
using Nito.AsyncEx;

namespace AI4E.Coordination.Utils
{
    public sealed class InMemoryPhysicalEndPoint : IPhysicalEndPoint<InMemoryPhysicalAddress>
    {
        private readonly AsyncProducerConsumerQueue<ValueMessage> _rxQueue = new AsyncProducerConsumerQueue<ValueMessage>();

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
