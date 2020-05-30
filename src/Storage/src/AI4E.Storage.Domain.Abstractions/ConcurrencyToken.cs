/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a concurrency-token that is oblivious to others and can be used to check for concurrent operations.
    /// </summary>
    /// <remarks>
    /// The domain storage uses two different strategies to check for concurrent access to entities 
    /// for online and offline transactions.
    /// For online transactions, a revision number is used that is increased each time the entity is changed.
    /// For offline transactions a concurrency-token is used instead of a revision number, because this has to be passed 
    /// outside to other system (for example to the client as a hidden HTML input element) so that it is not online 
    /// readable but may also be modified by others (for example a malicious user). While a revision has a certain
    /// structure and may just be increased as long as the operation succeeds, a concurrency-token does not suffer of
    /// this problem and is oblivious to the other, so that to maliciously modify it in order that an operation succeeds
    /// a random choice has to be token. This can be seen like a 'password' that needs to be correct to be allowed to
    /// modify the entity.
    /// 
    /// A default concurrency-token is specified to have the following meaning:
    /// When used in a request to change an entity indicated that no concurrency-token is specified. This may be a legal
    /// action for example when an entity is created and it therefore does not have a concurrency-token yet.
    /// An entity that does not have a concurrency-token can be modified without a concurrency-token specified or with
    /// a random concurrency-token specified. 
    /// </remarks>
    public readonly struct ConcurrencyToken : IEquatable<ConcurrencyToken>
    {
        /// <summary>
        /// Gets the default value that indicated that no concurrency-token is available.
        /// </summary>
        public static ConcurrencyToken NoConcurrencyToken { get; } = default;

        private readonly string? _rawValue;

        /// <summary>
        /// Creates a new instance of the <see cref="ConcurrencyToken"/> type from the specified raw underlying value.
        /// </summary>
        /// <param name="rawValue">A <see cref="string"/> defining the raw underlying value.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="rawValue"/> is <c>null</c>.</exception>
        public ConcurrencyToken(string rawValue)
        {
            if (rawValue is null)
                throw new ArgumentNullException(nameof(rawValue));

            _rawValue = rawValue;
        }

        /// <summary>
        /// Gets the <see cref="string"/> that is the raw underlying value.
        /// </summary>
        public string RawValue => _rawValue ?? string.Empty;

        /// <summary>
        /// Gets a boolean value indicating whether the current concurrency-token is the default value.
        /// </summary>
        public bool IsDefault => _rawValue is null || _rawValue.Length == 0;

        /// <inheritdoc/>
        public bool Equals(ConcurrencyToken other)
        {
            return other.RawValue.Equals(RawValue, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is ConcurrencyToken concurrencyToken && Equals(concurrencyToken);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return RawValue.GetHashCode(StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two concurrency-tokens are equal.
        /// </summary>
        /// <param name="left">The first <see cref="ConcurrencyToken"/>.</param>
        /// <param name="right">The second <see cref="ConcurrencyToken"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(ConcurrencyToken left, ConcurrencyToken right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two concurrency-tokens are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="ConcurrencyToken"/>.</param>
        /// <param name="right">The second <see cref="ConcurrencyToken"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(ConcurrencyToken left, ConcurrencyToken right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Implicitly converts the specified string value to a <see cref="ConcurrencyToken"/>.
        /// </summary>
        /// <param name="rawValue">A <see cref="string"/> defining the raw underlying value.</param>
        public static implicit operator ConcurrencyToken(string? rawValue)
        {
            return FromString(rawValue);
        }

        /// <summary>
        /// Returns the underlying raw string value that represents the concurrency-token.
        /// </summary>
        /// <returns>The raw concurrency-token value.</returns>
        public override string ToString()
        {
            return RawValue;
        }

        /// <summary>
        /// Returns a <see cref="ConcurrencyToken"/> from the specified raw string value.
        /// </summary>
        /// <param name="rawValue">A <see cref="string"/> defining the raw underlying value.</param>
        /// <returns>A <see cref="ConcurrencyToken"/> created from <paramref name="rawValue"/>.</returns>
        public static ConcurrencyToken FromString(string? rawValue)
        {
            if (rawValue is null)
            {
                return default;
            }

            return new ConcurrencyToken(rawValue);
        }
    }
}
