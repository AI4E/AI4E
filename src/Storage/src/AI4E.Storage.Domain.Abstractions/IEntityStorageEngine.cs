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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents the entity storage engine that is responsible for managing the underlying entity storage subsystem.
    /// </summary>
    public interface IEntityStorageEngine : ICommitAttemptExecutor, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Asynchronously loads the entity with the specified identifier.
        /// </summary>
        /// <param name="entityIdentifier">The entity identifier.</param>
        /// <param name="bypassCache">A boolean value indicating whether the internal caches shall be by-passed.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IEntityQueryResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entity load-result that describes the load operation status 
        /// and contains the entity on success.
        /// </returns>
        ValueTask<IEntityQueryResult> QueryEntityAsync(
            EntityIdentifier entityIdentifier,
            bool bypassCache,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously loads all entities of the specified type.
        /// </summary>
        /// <param name="entityType">The type of entity to load.</param>
        /// <param name="bypassCache">A boolean value indicating whether the internal caches shall be by-passed.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{ISuccessEntityLoadResult}"/> asynchronously enumerating the entity 
        /// load-results of all entity of type <paramref name="entityType"/> that are available.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="entityType"/> specifies a delegate type, a value-type,
        /// an interface type or an open generic type definition.
        /// </exception>
        IAsyncEnumerable<IFoundEntityQueryResult> QueryEntitiesAsync(
            Type entityType,
            bool bypassCache,
            CancellationToken cancellation = default);
    }
}
