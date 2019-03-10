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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    /// <summary>
    /// An abstraction of a database with minimal functionality.
    /// </summary>
    public interface IDatabase
    {
        /// <summary>
        /// Asynchronously tries to add the specified entry into the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be added.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was added successfully.
        /// </returns>
        /// <remarks>
        /// The entry is added successfully, if the database does not contain an entry with the same id than the specified entry.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        Task<bool> AddAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class;

        /// <summary>
        /// Asynchronously tries to update the specified entry in the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be updated in the database.</param>
        /// <param name="predicate">A predicate that the current database entry must match in order to perform the update operation.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was updated successfully.
        /// </returns>
        /// <remarks>
        /// The entry is updated successfully, if the database does contain an entry with the same id than the specified entry and it matched the predicate.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="entry"/> or <paramref name="predicate"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database does not support the specified predicate.</exception>
        Task<bool> UpdateAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default)
            where TEntry : class;

        /// <summary>
        /// Asynchronously tries to remove the specified entry from the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be removed from the database.</param>
        /// <param name="predicate">A predicate that the current database entry must match in order to perform the update operation.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was removed successfully.
        /// </returns>
        /// <remarks>
        /// The entry is removed successfully, if the database does contain an entry with the same id than the specified entry and it matched the predicate.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="entry"/> or <paramref name="predicate"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database does not support the specified predicate.</exception>
        Task<bool> RemoveAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default)
            where TEntry : class;

        Task Clear<TEntry>(CancellationToken cancellation = default)
            where TEntry : class;

        ValueTask<TEntry> GetOrAdd<TEntry>(TEntry entry, CancellationToken cancellation = default) 
            where TEntry : class;

        /// <summary>
        /// Asynchronously retrieves a collection of all stored entries that match the specified predicate.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="predicate">The predicate that the entries must match.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An async enumerable that enumerates all stored entries of type <typeparamref name="TEntry"/> that match the specified predicate.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database does not support the specified predicate.</exception>
        IAsyncEnumerable<TEntry> GetAsync<TEntry>(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default)
            where TEntry : class;

        ValueTask<TEntry> GetOneAsync<TEntry>(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default)
            where TEntry : class;

        IScopedDatabase CreateScope();

        bool SupportsScopes { get; }
    }

    /// <summary>
    /// An abstraction of a database with queryable functionality.
    /// </summary>
    public interface IQueryableDatabase : IDatabase
    {
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
        IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper, CancellationToken cancellation = default)
            where TEntry : class;
    }
}
