using System;
using System.Threading;
using AI4E.Remoting;

namespace AI4E.Coordination
{
    public sealed class SessionProvider<TAddress> : ISessionProvider
    {
        private readonly TAddress _address;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private int _counter = 0;

        public SessionProvider(IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                               IDateTimeProvider dateTimeProvider,
                               IAddressConversion<TAddress> addressConversion)
        {
            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _address = endPointMultiplexer.LocalAddress;

            if (_address.Equals(default(TAddress)))
                throw new ArgumentException($"The end-points physical address must not be the default value of '{typeof(TAddress)}'.", nameof(endPointMultiplexer));

            _dateTimeProvider = dateTimeProvider;
            _addressConversion = addressConversion;
        }

        public Session GetSession()
        {
            var count = Interlocked.Increment(ref _counter);
            var ticks = _dateTimeProvider.GetCurrentTime().Ticks + count;

            var prefix = BitConverter.GetBytes(ticks);
            var serializedAddress = _addressConversion.SerializeAddress(_address);

            return new Session(prefix, serializedAddress);
        }
    }
}
