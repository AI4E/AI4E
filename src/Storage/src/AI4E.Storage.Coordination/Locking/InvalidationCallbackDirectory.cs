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

// TODO: Add unit-tests

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Coordination.Locking
{
    /// <summary>
    /// A registry for callbacks that are invoked when an entry needs to be invalidated.
    /// </summary>
    public sealed class InvalidationCallbackDirectory : IInvalidationCallbackDirectory
    {
        private readonly ConcurrentDictionary<string, Func<CancellationToken, ValueTask>> _callbacks = new ConcurrentDictionary<string, Func<CancellationToken, ValueTask>>();

        /// <summary>
        /// Creates a new instance of the <see cref="InvalidationCallbackDirectory"/> type.
        /// </summary>
        public InvalidationCallbackDirectory() { }

        /// <inheritdoc/>
        public void Register(string key, Func<CancellationToken, ValueTask> callback)
        {
            _callbacks.AddOrUpdate(key, callback, (_, current) => current + callback);
        }

        /// <inheritdoc/>
        public void Unregister(string key, Func<CancellationToken, ValueTask> callback)
        {
            while (_callbacks.TryGetValue(key, out var current) && !_callbacks.TryUpdate(key, current + callback, current)) { }
        }

        /// <inheritdoc/>
        public ValueTask InvokeAsync(string key, CancellationToken cancellation)
        {
            if (!_callbacks.TryGetValue(key, out var callback))
            {
                return default;
            }

            var invocationList = callback.GetInvocationList();
            return new ValueTask(Task.WhenAll(invocationList.Select(p => ((Func<CancellationToken, ValueTask>)p)(cancellation).AsTask()))); // TODO: Use ValueTaskHelper
        }
    }
}
