using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public sealed class NoDatabaseScope : IDatabaseScope
    {
        private readonly Dictionary<Type, object> _entries = new Dictionary<Type, object>();

        private static IEqualityComparer<TEntry> GetEqualityComparer<TEntry>()
            where TEntry : class
        {
            return EntryEqualityComparer<TEntry>.Instance;
        }

        private void ClearEntries()
        {
            _entries.Clear();
        }

        private bool TryGetEntries<TEntry>([NotNullWhen(true)] out ICollection<TEntry>? entries)
            where TEntry : class
        {
            entries = default;
            return _entries.TryGetValue(typeof(TEntry), out Unsafe.As<ICollection<TEntry>, object>(ref entries!)!);
        }

        private ICollection<TEntry> GetEntries<TEntry>()
            where TEntry : class
        {
            HashSet<TEntry> entries = default!;

            if (!_entries.TryGetValue(typeof(TEntry), out Unsafe.As<HashSet<TEntry>, object>(ref entries!)!))
            {
                entries = new HashSet<TEntry>(GetEqualityComparer<TEntry>());
                _entries.Add(typeof(TEntry), entries);
            }

            return entries;
        }

        #region IDatabaseScope

        /// <inheritdoc />
        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            if (!TryGetEntries<TEntry>(out var entries))
            {
                return AsyncEnumerable.Empty<TEntry>();
            }

            return entries
                .Where(predicate.Compile(preferInterpretation: true))
                .ToAsyncEnumerable();
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
            Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (queryShaper is null)
                throw new ArgumentNullException(nameof(queryShaper));

            if (!TryGetEntries<TEntry>(out var entries))
            {
                return AsyncEnumerable.Empty<TResult>();
            }

            return queryShaper(entries.AsQueryable()).ToAsyncEnumerable();
        }

        /// <inheritdoc />
        public ValueTask RemoveAsync<TEntry>(
            TEntry entry,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (TryGetEntries<TEntry>(out var entries))
            {
                entries.Remove(entry);
            }

            return default;
        }

        /// <inheritdoc />
        public ValueTask StoreAsync<TEntry>(
            TEntry entry,
            CancellationToken cancellation)
            where TEntry : class
        {
            var entries = GetEntries<TEntry>();

            // Remove any old entry with the same identity and add our value.
            entries.Remove(entry);
            entries.Add(entry);

            return default;
        }

        /// <inheritdoc />
        public ValueTask RollbackAsync(CancellationToken cancellation)
        {
            ClearEntries();
            return default;
        }

        /// <inheritdoc />
        public ValueTask<bool> TryCommitAsync(CancellationToken cancellation)
        {
            ClearEntries();
            return new ValueTask<bool>(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ClearEntries();
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            ClearEntries();
            return default;
        }

        #endregion
    }
}
