using System;
using System.Text;
using System.Threading;
using AI4E.Remoting;
using AI4E.Utils;

namespace AI4E.Storage.Coordination
{
    public sealed class SessionProvider<TAddress> : ISessionProvider
    {
        private readonly TAddress _address;
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IDateTimeProvider _dateTimeProvider;
        private int _counter = 0;

        public SessionProvider(IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                               IDateTimeProvider dateTimeProvider)
        {
            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _address = endPointMultiplexer.LocalAddress;

            if (_address.Equals(default(TAddress)))
                throw new ArgumentException($"The end-points physical address must not be the default value of '{typeof(TAddress)}'.", nameof(endPointMultiplexer));

            _endPointMultiplexer = endPointMultiplexer;
            _dateTimeProvider = dateTimeProvider;
        }

        public Session GetSession()
        {
            var count = Interlocked.Increment(ref _counter);
            var ticks = _dateTimeProvider.GetCurrentTime().Ticks + count;

            var prefix = BitConverter.GetBytes(ticks);
            var serializedAddress = Encoding.UTF8.GetBytes(_endPointMultiplexer.AddressToString(_address)); // TODO: This allocates

            return new Session(prefix, serializedAddress);
        }
    }
}
