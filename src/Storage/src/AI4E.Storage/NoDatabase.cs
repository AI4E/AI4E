using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public sealed class NoDatabase : IDatabase
    {
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
