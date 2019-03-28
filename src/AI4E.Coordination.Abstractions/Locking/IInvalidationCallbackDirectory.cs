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
using System.Threading.Tasks;

namespace AI4E.Coordination.Locking
{
    /// <summary>
    /// A registry for callbacks that are invoked when an entry needs to be invalidated.
    /// </summary>
    public interface IInvalidationCallbackDirectory
    {
        /// <summary>
        /// Registers a callback for the specified entry.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <param name="callback">The invalidation callback.</param>
        void Register(string key, Func<CancellationToken, ValueTask> callback);

        /// <summary>
        /// Unregistersa callback for the specified entry.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <param name="callback">The invalidation callback.</param>
        void Unregister(string key, Func<CancellationToken, ValueTask> callback);

        /// <summary>
        /// Asynchronously invokes all calbacks of the specified entry.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation that completed,
        /// when all callbacks are completed.
        /// </returns>
        ValueTask InvokeAsync(string key, CancellationToken cancellation = default);
    }
}
