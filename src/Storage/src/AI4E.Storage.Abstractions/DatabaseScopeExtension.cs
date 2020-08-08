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
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    /// <summary>
    /// Contains extension methods for the <see cref="IDatabaseScope"/> type.
    /// </summary>
    public static class DatabaseScopeExtension
    {
        /// <summary>
        /// Asynchronously retrieves a collection of all stored entries.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="databaseScope">The database scope.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{TEntry}"/> that enumerates all stored entries of type 
        /// <typeparamref name="TEntry"/> 
        /// that match the specified predicate.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown of <paramref name="databaseScope"/> is <c>null</c>.
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
        public static IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            this IDatabaseScope databaseScope,
            CancellationToken cancellation = default) where TEntry : class
        {
            if (databaseScope is null)
                throw new ArgumentNullException(nameof(databaseScope));

            return databaseScope.GetAsync<TEntry>(_ => true, cancellation);
        }

        /// <summary>
        /// Asynchronously retrieves a single entry that matches the specified predicate.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="databaseScope">The database scope.</param>
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
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="databaseScope"/> or <paramref name="predicate"/> is <c>null</c>.
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
        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabaseScope databaseScope,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (databaseScope is null)
                throw new ArgumentNullException(nameof(databaseScope));

            return databaseScope.GetAsync(predicate, cancellation).FirstOrDefaultAsync(cancellation)!;
        }

        /// <summary>
        /// Asynchronously retrieves a single entry.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="databaseScope">The database scope.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TEntry}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains an entry of the specified type or
        /// <c>null</c> if no entry matched the specified type.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="databaseScope"/> is <c>null</c>.
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
        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabaseScope databaseScope,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (databaseScope is null)
                throw new ArgumentNullException(nameof(databaseScope));

            return databaseScope.GetOneAsync<TEntry>(_ => true, cancellation);
        }

        /// <summary>
        /// Asynchronously stores the specified entry in the database overriding any existing entry with the same 
        /// identity.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="databaseScope">The database scope.</param>
        /// <param name="entry">The entry that shall be stored into the database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns> A <see cref="ValueTask"/> that represents the asynchronous operation. </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="databaseScope"/> or <paramref name="entry"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        public static async ValueTask StoreAsync<TEntry>(
            this IDatabaseScope databaseScope,
            TEntry entry,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (databaseScope is null)
                throw new ArgumentNullException(nameof(databaseScope));

            // TODO: This is not guaranteed to succeed, as not only the predicate is checked but also the success 
            // of the database operation ie. transaction abort, etc.? => Return ValueTask<bool> instead of ValueTask
            await databaseScope.StoreAsync(entry, _ => true, cancellation).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously removes the specified entry from the database.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="databaseScope">The database scope.</param>
        /// <param name="entry">The entry that shall be removed from the database.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="databaseScope"/> or <paramref name="entry"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="StorageException">
        /// Thrown if an unresolvable exception occurs in the storage subsystem.
        /// </exception>
        /// <exception cref="StorageUnavailableException">
        /// Thrown if the storage subsystem is unavailable or unreachable.
        /// </exception>
        public static async ValueTask RemoveAsync<TEntry>(
            this IDatabaseScope databaseScope,
            TEntry entry,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (databaseScope is null)
                throw new ArgumentNullException(nameof(databaseScope));

            // TODO: This is not guaranteed to succeed, as not only the predicate is checked but also the success 
            // of the database operation ie. was there an entry to remove, transaction abort, etc.?
            // => Return ValueTask<bool> instead of ValueTask
            await databaseScope.RemoveAsync(entry, _ => true, cancellation).ConfigureAwait(false);
        }
    }
}
