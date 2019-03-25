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
using System.Threading;
using AI4E.Remoting;

namespace AI4E.Coordination.Session
{
    /// <summary>
    /// Provides session identifiers based on the address of the current physical end point.
    /// </summary>
    /// <typeparam name="TAddress">The type of address the system uses.</typeparam>
    public sealed class SessionProvider<TAddress> : ISessionProvider
    {
        private readonly TAddress _address;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private int _counter = 0;

        /// <summary>
        /// Creates a new instance of the <see cref="SessionProvider{TAddress}"/> type.
        /// </summary>
        /// <param name="endPointMultiplexer">The <see cref="IPhysicalEndPointMultiplexer{TAddress}"/> used to obain the current address.</param>
        /// <param name="dateTimeProvider">A <see cref="IDateTimeProvider"/> used to get the current date and time.</param>
        /// <param name="addressConversion">An <see cref="IAddressConversion{TAddress}"/> that is used to convert the address to bytes.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="endPointMultiplexer"/>, <paramref name="dateTimeProvider"/>
        /// or <paramref name="addressConversion"/> is <c> null.</c>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the address returned by the <see cref="IPhysicalEndPointMultiplexer{TAddress}.LocalAddress"/> property of
        /// <paramref name="endPointMultiplexer"/> returns the default value of type <typeparamref name="TAddress"/>.
        /// </exception>
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

        /// <inheritdoc/>
        public CoordinationSession GetSession()
        {
            var count = Interlocked.Increment(ref _counter);
            var ticks = _dateTimeProvider.GetCurrentTime().Ticks + count;

            var prefix = BitConverter.GetBytes(ticks);
            var serializedAddress = _addressConversion.SerializeAddress(_address);

            return new CoordinationSession(prefix, serializedAddress);
        }
    }
}
