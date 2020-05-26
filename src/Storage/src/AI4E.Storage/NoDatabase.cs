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
    /// Represents a NULL-Object for the <see cref="IDatabase"/> abstraction.
    /// </summary>
    public sealed class NoDatabase : IDatabase
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="NoDatabase"/> type.
        /// </summary>
        public static NoDatabase Instance { get; } = new NoDatabase();

        private NoDatabase() { }

        /// <inheritdoc />
        public bool SupportsScopes => true;

        /// <inheritdoc />
        public ValueTask<bool> AddAsync<TEntry>(TEntry entry, CancellationToken cancellation)
            where TEntry : class
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            return new ValueTask<bool>(false);
        }

        /// <inheritdoc />
        public ValueTask Clear<TEntry>(CancellationToken cancellation)
            where TEntry : class
        {
            return default;
        }

        /// <inheritdoc />
        public IDatabaseScope CreateScope()
        {
            return new NoDatabaseScope();
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return AsyncEnumerable.Empty<TEntry>();
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
            Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (queryShaper is null)
                throw new ArgumentNullException(nameof(queryShaper));

            return AsyncEnumerable.Empty<TResult>();
        }

        /// <inheritdoc />
        public ValueTask<bool> RemoveAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return new ValueTask<bool>(false);
        }

        /// <inheritdoc />
        public ValueTask<bool> UpdateAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return new ValueTask<bool>(false);
        }
    }
}
