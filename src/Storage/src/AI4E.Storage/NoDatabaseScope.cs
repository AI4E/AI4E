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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    /// <summary>
    /// Represents a NULL-Object for the <see cref="IDatabaseScope"/> abstraction.
    /// </summary>
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

        private bool TryGetEntries<TEntry>([NotNullWhen(true)] out HashSet<TEntry>? entries)
            where TEntry : class
        {
            entries = default;
            return _entries.TryGetValue(typeof(TEntry), out Unsafe.As<HashSet<TEntry>, object>(ref entries!)!);
        }

        private HashSet<TEntry> GetEntries<TEntry>()
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
        public ValueTask<bool> RemoveAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry?, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            if (TryGetEntries<TEntry>(out var entries))
            {
#if NETSTD20
                TEntry? storedEntry = null;

                foreach(var e in entries)
                {
                    if(EntryEqualityComparer<TEntry>.Instance.Equals(e, entry))
                    {
                        storedEntry = e;
                    }
                }
#else
                if (!entries.TryGetValue(entry, out var storedEntry))
                {
                    storedEntry = null!;
                }
#endif
                var compiledPredicate = predicate.Compile(preferInterpretation: true);
                if (compiledPredicate(storedEntry))
                {
                    entries.Remove(entry);
                    return new ValueTask<bool>(true);
                }
            }

            return new ValueTask<bool>(false);
        }

        /// <inheritdoc />
        public ValueTask<bool> StoreAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry?, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var entries = GetEntries<TEntry>();

#if NETSTD20
            TEntry? storedEntry = null;

            foreach (var e in entries)
            {
                if (EntryEqualityComparer<TEntry>.Instance.Equals(e, entry))
                {
                    storedEntry = e;
                }
            }
#else
            if (!entries.TryGetValue(entry, out var storedEntry))
            {
                storedEntry = null!;
            }
#endif

            var compiledPredicate = predicate.Compile(preferInterpretation: true);
            if (compiledPredicate(storedEntry))
            {
                if (storedEntry != null)
                {
                    // Remove any old entry with the same identity and add our value.
                    entries.Remove(entry);
                }

                entries.Add(entry);
                return new ValueTask<bool>(true);
            }

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
