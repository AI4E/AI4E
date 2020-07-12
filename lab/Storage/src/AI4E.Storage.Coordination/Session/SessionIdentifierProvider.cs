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
using System.Text;
using System.Threading;
using AI4E.Remoting;
using AI4E.Utils;

namespace AI4E.Storage.Coordination.Session
{
    /// <summary>
    /// Provides session identifiers based on the address of the current physical end point.
    /// </summary>
    /// <typeparam name="TAddress">The type of address the system uses.</typeparam>
    public sealed class SessionIdentifierProvider<TAddress> : ISessionIdentifierProvider
    {
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IDateTimeProvider _dateTimeProvider;
        private int _counter = 0;

        /// <summary>
        /// Creates a new instance of the <see cref="SessionIdentifierProvider{TAddress}"/> type.
        /// </summary>
        /// <param name="endPointMultiplexer">The <see cref="IPhysicalEndPointMultiplexer{TAddress}"/> used to obain the current address.</param>
        /// <param name="dateTimeProvider">A <see cref="IDateTimeProvider"/> used to get the current date and time.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="endPointMultiplexer"/> or <paramref name="dateTimeProvider"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the address returned by the <see cref="IPhysicalEndPointMultiplexer{TAddress}.LocalAddress"/> property of
        /// <paramref name="endPointMultiplexer"/> returns the default value of type <typeparamref name="TAddress"/>.
        /// </exception>
        public SessionIdentifierProvider(
            IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
            IDateTimeProvider dateTimeProvider)
        {
            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            // TODO: Do we have to check this here or does this have to be done in the implementation of IPhysicalEndPointMultiplexer?
            if (endPointMultiplexer.LocalAddress.Equals(default(TAddress)))
                throw new ArgumentException($"The end-points physical address must not be the default value of '{typeof(TAddress)}'.", nameof(endPointMultiplexer));

            _endPointMultiplexer = endPointMultiplexer;
            _dateTimeProvider = dateTimeProvider;
        }

        /// <inheritdoc/>
        public SessionIdentifier CreateUniqueSessionIdentifier()
        {
            var count = Interlocked.Increment(ref _counter);
            var ticks = _dateTimeProvider.GetCurrentTime().Ticks + count;

            var prefix = BitConverter.GetBytes(ticks); // TODO: This allocates
            var stringifiedAddress = _endPointMultiplexer.AddressToString(_endPointMultiplexer.LocalAddress);
            var serializedAddress = Encoding.UTF8.GetBytes(stringifiedAddress); // TODO: This allocates

            return new SessionIdentifier(prefix, serializedAddress);
        }
    }
}
