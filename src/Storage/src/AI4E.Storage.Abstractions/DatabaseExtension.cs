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
using AI4E.Internal;

namespace AI4E.Storage
{
    public static partial class DatabaseExtension
    {
        /// <summary>
        /// Asynchronously retrieves a collection of all stored entries.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An async enumerable that enumerates all stored entries of type <typeparamref name="TEntry"/>.
        /// </returns>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        public static IAsyncEnumerable<TEntry> GetAsync<TEntry>(this IDatabase database, CancellationToken cancellation = default)
             where TEntry : class
        {
#pragma warning disable CA1062
            return database.GetAsync<TEntry>(_ => true, cancellation);
#pragma warning restore CA1062
        }

        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabase database,
           Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
    where TEntry : class
        {
#pragma warning disable CA1062
            return database.GetAsync(predicate, cancellation).FirstOrDefaultAsync(cancellation)!;
#pragma warning restore CA1062
        }

        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabase database,
            CancellationToken cancellation = default)
            where TEntry : class
        {
#pragma warning disable CA1062
            return database.GetOneAsync<TEntry>(_ => true, cancellation);
#pragma warning restore CA1062
        }

        public static async ValueTask<TEntry> GetOrAdd<TEntry>(this IDatabase database, TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

#pragma warning disable CA1062 
            while (!await database.AddAsync(entry, cancellation).ConfigureAwait(false))
#pragma warning restore CA1062
            {
                var result = await database.GetOneAsync(DataPropertyHelper.BuildPredicate(entry), cancellation);

                if (result != null)
                    return result;
            }

            return entry;
        }

        public static async ValueTask AddOrUpdate<TEntry>(this IDatabase database, TEntry entry, CancellationToken cancellation = default)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            while (!await database.AddAsync(entry, cancellation).ConfigureAwait(false))
            {
                var success = await database.UpdateAsync(entry, cancellation);

                if (success)
                    return;
            }
        }
    }
}
