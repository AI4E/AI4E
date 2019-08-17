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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Storage
{
    /// <summary>
    /// Represents the storage for session entries.
    /// </summary>
    public interface ISessionStorage
    {
        /// <summary>
        /// Asynchronously retrieves the session entry with the specified session identifier.
        /// </summary>
        /// <param name="session">The entry key.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the loaded entry, or <c>null</c>
        /// if an entry with <paramref name="session"/> does not exist.
        /// </returns>
        Task<IStoredSession> GetSessionAsync(SessionIdentifier session, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously retrieves a list of entries representing all known sessions.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that iterates the entries of all known sessions.
        /// </returns>
        IAsyncEnumerable<IStoredSession> GetSessionsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously replaces an existing session entry.
        /// </summary>
        /// <param name="value">The value that shall replace the existing.</param>
        /// <param name="comparand">The comparand that the existing entry must equal.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entry that was present before the replacement, if executed.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if either both, <paramref name="comparand"/> and <paramref name="value"/> are <c>null</c>
        /// or both, <paramref name="comparand"/> and <paramref name="value"/> are not <c>null</c> and the identifiers are not equal.
        /// </exception>
        /// <remarks>
        /// If the tasks result equals comparand, the replacement was executed, otherwise no entry was replaced.
        /// </remarks>
        Task<IStoredSession> UpdateSessionAsync(
            IStoredSession value, IStoredSession comparand, CancellationToken cancellation = default);
    }
}
