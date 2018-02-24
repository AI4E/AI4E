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

namespace AI4E.Modularity
{
    public sealed class EndPointRoute : IEquatable<EndPointRoute>
    {
        private static readonly ConcurrentDictionary<string, EndPointRoute> _routes = new ConcurrentDictionary<string, EndPointRoute>();

        private string _route;

        public EndPointRoute(string route)
        {
            _route = route;
        }

        public string Route => _route;

        public static EndPointRoute CreateRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentNullOrWhiteSpaceException(nameof(route));
            }

            return _routes.GetOrAdd(route, _ => new EndPointRoute(route));
        }

        public bool Equals(EndPointRoute other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(other, this))
                return true;

            return other._route == _route;
        }

        public override bool Equals(object obj)
        {
            return obj is EndPointRoute route && Equals(route);
        }

        public override int GetHashCode()
        {
            return _route.GetHashCode();
        }

        public override string ToString()
        {
            return _route;
        }

        public static bool operator ==(EndPointRoute left, EndPointRoute right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(EndPointRoute left, EndPointRoute right)
        {
            if (ReferenceEquals(left, null))
            {
                return !ReferenceEquals(right, null);
            }

            return !left.Equals(right);
        }
    }
}
