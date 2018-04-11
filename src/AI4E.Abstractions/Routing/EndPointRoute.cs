/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EndPointRoute.cs 
 * Types:           AI4E.Routing.EndPointRoute
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Concurrent;

namespace AI4E.Routing
{
    /// <summary>
    /// Represents the route to a virtual end point. (I.e. its name)
    /// </summary>
    [Serializable]
    public sealed class EndPointRoute : IEquatable<EndPointRoute>
    {
        private static readonly ConcurrentDictionary<string, EndPointRoute> _routes = new ConcurrentDictionary<string, EndPointRoute>();

        private string _route;

        // TODO: private -> Fix serialization
        public EndPointRoute(string route)
        {
            _route = route;
        }

        /// <summary>
        /// Gets a stringified version of route.
        /// </summary>
        public string Route => _route;

        /// <summary>
        /// Creates a new route from the specified string.
        /// </summary>
        /// <param name="route">The string that specifies the route.</param>
        /// <returns>The created route.</returns>
        /// <exception cref="ArgumentNullOrWhiteSpaceException">Thrown if <paramref name="route"/> is either null, an empty string or a string consisting of whitespace only.</exception>
        public static EndPointRoute CreateRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentNullOrWhiteSpaceException(nameof(route));
            }

            return _routes.GetOrAdd(route, _ => new EndPointRoute(route));
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specifies route equals the current instance.
        /// </summary>
        /// <param name="other">The route to compare to.</param>
        /// <returns>True if <paramref name="other"/> equals the current route, false otherwise.</returns>
        public bool Equals(EndPointRoute other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(other, this))
                return true;

            return other._route == _route;
        }

        /// <summary>
        /// Return a boolean value indicating whether the specifies object equals the current route.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>True if <paramref name="obj"/> is of type <see cref="EndPointRoute"/> and equals the current route, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is EndPointRoute route && Equals(route);
        }

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <returns>The generated hash code.</returns>
        public override int GetHashCode()
        {
            return _route.GetHashCode();
        }

        /// <summary>
        /// Returns a stringified version of the route.
        /// </summary>
        /// <returns>A string representing the current route.</returns>
        public override string ToString()
        {
            return _route;
        }

        /// <summary>
        /// Returns a boolean value indicating whether two routes are equal.
        /// </summary>
        /// <param name="left">The first route.</param>
        /// <param name="right">The second route.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(EndPointRoute left, EndPointRoute right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two routes are inequal.
        /// </summary>
        /// <param name="left">The first route.</param>
        /// <param name="right">The second route.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(EndPointRoute left, EndPointRoute right)
        {
            if (left is null)
                return !(right is null);

            return !left.Equals(right);
        }
    }
}
