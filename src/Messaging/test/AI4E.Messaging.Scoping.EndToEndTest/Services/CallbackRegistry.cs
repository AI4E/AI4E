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

using System.Collections.Generic;
using System.Collections.Immutable;
using AI4E.Messaging.Routing;

namespace AI4E.Messaging.Scoping.EndToEndTest.Services
{
    public sealed class CallbackRegistry
    {
        private readonly HashSet<RouteEndPointScope> _callbacks;
        private readonly object _mutex = new object();

        public CallbackRegistry()
        {
            _callbacks = new HashSet<RouteEndPointScope>();
        }

        public void RegisterCallback(RouteEndPointScope scope)
        {
            lock (_mutex)
            {
                _callbacks.Add(scope);
            }
        }

        public void UnregisterCallback(RouteEndPointScope scope)
        {
            lock (_mutex)
            {
                _callbacks.Remove(scope);
            }
        }

        public IEnumerable<RouteEndPointScope> Callbacks
        {
            get
            {
                lock (_mutex)
                {
                    return _callbacks.ToImmutableList(); // We could optimize this but this is only a sample.
                }
            }
        }
    }
}
