/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    /// <summary>
    /// A transactional database scope.
    /// </summary>
    public interface IDatabaseScope : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Asynchronously stores the specified entry in the database overriding any existing entry with the same identity.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be stored into the database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A value task that represents the asynchronous operation. 
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        ValueTask StoreAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class;

        /// <summary>
        /// Asynchronously removes the specified entry from the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be removed from the database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        ValueTask RemoveAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class;

        /// <summary>
        /// Asynchronously retrieves a collection of all stored entries that match the specified predicate.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="predicate">The predicate that the entries must match.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An async enumerable that enumerates all stored entries of type <typeparamref name="TEntry"/> that match the specified predicate.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database does not support the specified predicate.</exception>
        IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class;

        public ValueTask<TEntry?> GetOneAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            return DatabaseScopeExtension.GetOneAsync(this, predicate, cancellation);
        }

        /// <summary>
        /// Asynchronously tries to commit the changes to the database.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the commit operation was performed successfully.
        /// </returns>
        ValueTask<bool> TryCommitAsync(CancellationToken cancellation = default); // TODO: Specify whether we roll-back on failure.

        /// <summary>
        /// Asynchronously rolls back all changes.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
        ValueTask RollbackAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously performs a database query specified by a query shaper.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <param name="queryShaper">A function that specifies the database query.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An async enumerable that enumerates items of type <typeparamref name="TResult"/> that are the query result.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queryShaper"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database does not support the specified query.</exception>
        public async IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
            Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
            [EnumeratorCancellation]  CancellationToken cancellation = default)
            where TEntry : class
        {
            if (queryShaper is null)
                throw new ArgumentNullException(nameof(queryShaper));

            // TODO: Add a default implementation that intelligently falls back to filtering and in-memory processing. 

            var entries = await GetAsync<TEntry>(_ => true, cancellation);
            var queryable = entries.AsQueryable();
            foreach (var entry in queryShaper(queryable))
            {
                yield return entry;
            }
        }
    }
}
