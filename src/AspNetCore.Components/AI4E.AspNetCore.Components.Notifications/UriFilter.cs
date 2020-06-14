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

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// Represents an uri filter.
    /// </summary>
    public readonly struct UriFilter : IEquatable<UriFilter>
    {
        private readonly string _uri;
        private readonly bool _exactMatch;

        public static UriFilter MatchAll => default;

        /// <summary>
        /// Creates a new instance of the <see cref="UriFilter"/> type.
        /// </summary>
        /// <param name="uri">The uri that represets the filter.</param>
        /// <param name="exactMatch">A boolean value indicating whether the uris must match exactly.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is null.</exception>
        public UriFilter(string uri, bool exactMatch = false)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            _uri = uri.Trim();

            if (!_uri.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                _uri = "/" + _uri;
            }

            _exactMatch = exactMatch;
        }

        public UriFilter(Uri uri, bool exactMatch = false)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            _uri = uri.ToString();

            if (!_uri.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                _uri = "/" + _uri;
            }

            _exactMatch = exactMatch;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified uri filter matches the current one.
        /// </summary>
        /// <param name="other">The <see cref="UriFilter"/> to compare with.</param>
        /// <returns>True if <paramref name="other"/> matches the current uri filter, false otherwise.</returns>
        public bool Equals(UriFilter other)
        {
            return _exactMatch == other._exactMatch &&
                   _uri == other._uri;
        }

        /// <summary>
        /// return a boolean value inidcating whether the specified object matched the current uri filter.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with.</param>
        /// <returns>True if <paramref name="obj"/> is of type <see cref="UriFilter"/> and equals the current uri filter, false otherwise.</returns>
        public override bool Equals(object? obj)
        {
            return obj is UriFilter uriFilter && Equals(uriFilter);
        }

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <returns>The hash code for the current instance.</returns>
        public override int GetHashCode()
        {
            return (_uri, _exactMatch).GetHashCode();
        }

        /// <summary>
        /// Compares two uri filters.
        /// </summary>
        /// <param name="left">The first <see cref="UriFilter"/>.</param>
        /// <param name="right">The second <see cref="UriFilter"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in UriFilter left, in UriFilter right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two uri filters for inequality.
        /// </summary>
        /// <param name="left">The first <see cref="UriFilter"/>.</param>
        /// <param name="right">The second <see cref="UriFilter"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in UriFilter left, in UriFilter right)
        {
            return !left.Equals(right);
        }

        public bool IsMatch(Uri uri)
        {
            if (uri is null)
                throw new ArgumentNullException(nameof(uri));

#pragma warning disable CA2234
            return IsMatch(uri.ToString());
#pragma warning restore CA2234
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified url matches the filter.
        /// </summary>
        /// <param name="uri">The uri to test.</param>
        /// <returns>True if <paramref name="uri"/> matches the filter, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is null.</exception>
        public bool IsMatch(string uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            if (Equals(default))
            {
                return true;
            }

            var comparison = _uri.AsSpan();

            if (!uri.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                comparison = comparison.Slice(1);
            }

            if (_exactMatch)
            {
                return comparison.Equals(uri.AsSpan(), StringComparison.Ordinal);
            }
            else
            {
                return uri.AsSpan().StartsWith(comparison, StringComparison.Ordinal);
            }
        }
    }
}
