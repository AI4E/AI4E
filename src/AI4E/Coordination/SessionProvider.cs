using System;
using AI4E.Remoting;

namespace AI4E.Coordination
{
    public sealed class SessionProvider<TAddress> : ISessionProvider
    {
        private readonly TAddress _address;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAddressConversion<TAddress> _addressConversion;

        public SessionProvider(IEndPointMultiplexer<TAddress> endPointMultiplexer,
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
            _dateTimeProvider = dateTimeProvider;
            _addressConversion = addressConversion;
        }

        public string GetSession()
        {
            return SessionHelper.GetNextSessionFromAddress(_address, _addressConversion, _dateTimeProvider);
        }
    }
}
