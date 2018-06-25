using System;
using System.Threading;
using AI4E.Remoting;

namespace AI4E.Coordination
{
    internal static class SessionHelper
    {
        private static int _counter = 0;

        // Creates a new unique session identifier for the specified address.
        public static string GetNextSessionFromAddress<TAddress>(TAddress address, IAddressConversion<TAddress> addressConversion, IDateTimeProvider dateTimeProvider)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            // The session is mainly the local physical address 
            // combined with a prefix to distinguish between sessions 
            // with the same physical address that live one after another.

            // The prefix is the current timestamp with a discriminator 
            // added to distinguish between sessions created at the same time.

            var count = Interlocked.Increment(ref _counter);
            var ticks = dateTimeProvider.GetCurrentTime().Ticks + count;

            var prefix = BitConverter.GetBytes(ticks);
            var serializedAddress = addressConversion.SerializeAddress(address);

            var arr = new byte[prefix.Length + serializedAddress.Length];

            Array.Copy(prefix, arr, prefix.Length);
            Array.Copy(serializedAddress, 0, arr, prefix.Length, serializedAddress.Length);

            return Convert.ToBase64String(arr);
        }

        public static TAddress GetAddressFromSession<TAddress>(string session, IAddressConversion<TAddress> addressConversion)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            var arr = Convert.FromBase64String(session);

            var serializedAddress = new byte[arr.Length - 8];

            Array.Copy(arr, 8, serializedAddress, 0, serializedAddress.Length);

            return addressConversion.DeserializeAddress(serializedAddress);
        }
    }
}
