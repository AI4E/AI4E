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
    /// An abstraction of a database with basic functionality.
    /// </summary>
    public interface IDatabase
    {
        /// <summary>
        /// Asynchronously tries to add the specified entry into the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be added.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Boolean}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was added 
        /// successfully.
        /// </returns>
        /// <remarks>
        /// The entry is added successfully, if the database does not contain an entry with the same id than the 
        /// specified entry.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is <c>null</c>.</exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        ValueTask<bool> AddAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class;

        /// <summary>
        /// Asynchronously tries to update the specified entry in the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be updated in the database.</param>
        /// <param name="predicate">
        /// A predicate that the current database entry must match in order to perform the update operation.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Boolean}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was updated 
        /// successfully.
        /// </returns>
        /// <remarks>
        /// The entry is updated successfully, if the database does contain an entry with the same id than the 
        /// specified entry and it matched the predicate.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entry"/> or <paramref name="predicate"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified predicate.
        /// </exception>
        ValueTask<bool> UpdateAsync<TEntry>(
            TEntry entry, 
            Expression<Func<TEntry, bool>> predicate, 
            CancellationToken cancellation = default) where TEntry : class;

        /// <summary>
        /// Asynchronously tries to remove the specified entry from the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry that shall be removed from the database.</param>
        /// <param name="predicate">A predicate that the current database entry must match in order to perform the 
        /// update operation.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Boolean}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was removed 
        /// successfully.
        /// </returns>
        /// <remarks>
        /// The entry is removed successfully, if the database does contain an entry with the same id than the 
        /// specified entry and it matched the predicate.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entry"/> or <paramref name="predicate"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified predicate.
        /// </exception>
        ValueTask<bool> RemoveAsync<TEntry>(
            TEntry entry, 
            Expression<Func<TEntry, bool>> predicate, 
            CancellationToken cancellation = default) where TEntry : class;

        /// <summary>
        /// Asynchronously removes all entries of the specified type from the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask Clear<TEntry>(CancellationToken cancellation = default)
            where TEntry : class;

        /// <summary>
        /// Asynchronously retrieves a collection of all stored entries that match the specified predicate.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="predicate">The predicate that the entries must match.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An async enumerable that enumerates all stored entries of type <typeparamref name="TEntry"/> 
        /// that match the specified predicate.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is <c>null</c>.</exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified predicate.
        /// </exception>
        IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate, 
            CancellationToken cancellation = default) where TEntry : class;

        /// <summary>
        /// Asynchronously retrieves a single entry that matches the specified predicate.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="predicate">The predicate that the entry must match.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TEntry}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains an entry that matches <paramref name="predicate"/> or
        /// <c>null</c> if no entry matched <paramref name="predicate"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is <c>null</c>.</exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the database does not support the specified predicate.
        /// </exception>
        public ValueTask<TEntry?> GetOneAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate, 
            CancellationToken cancellation = default) where TEntry : class
        {
            return DatabaseExtension.GetOneAsync(this, predicate, cancellation);
        }

        /// <summary>
        /// Creates a <see cref="IDatabaseScope"/> that can be used to perform multiple operations atomically.
        /// </summary>
        /// <returns>The created <see cref="IDatabaseScope"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if <see cref="SupportsScopes"/> is false.</exception>
        public IDatabaseScope CreateScope()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets a boolean value indicating whether the database supports scoping.
        /// </summary>
        public bool SupportsScopes => false;

        /// <summary>
        /// Asynchronously performs a database query specified by a query shaper.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <param name="queryShaper">A function that specifies the database query.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{TResult}"/> that enumerates items of type <typeparamref name="TResult"/> 
        /// that are the query result.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queryShaper"/> is null.</exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database does not support the specified query.</exception>
        
        public async IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
            Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
            [EnumeratorCancellation] CancellationToken cancellation = default)
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
