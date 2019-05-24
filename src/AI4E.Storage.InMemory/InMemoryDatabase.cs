/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using Nito.AsyncEx;

namespace AI4E.Storage.InMemory
{
    /// <summary>
    /// Represents an in-memory database.
    /// </summary>
    public sealed class InMemoryDatabase : IDatabase
    {
        private readonly ConcurrentDictionary<Type, object> _typedStores = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Creates a new instance of the <see cref="InMemoryDatabase"/> type.
        /// </summary>
        public InMemoryDatabase()
        { }

        private IInMemoryDatabase<TEntry> GetTypedStore<TEntry>()
            where TEntry : class
        {
            return (IInMemoryDatabase<TEntry>)_typedStores.GetOrAdd(typeof(TEntry), _ => CreateTypedStore<TEntry>());
        }

        private IInMemoryDatabase<TEntry> CreateTypedStore<TEntry>()
            where TEntry : class
        {
            var dataType = typeof(TEntry);
            var idType = DataPropertyHelper.GetIdType<TEntry>();

            if (idType == null)
            {
                throw new InvalidOperationException($"Cannot store objects of type '{typeof(TEntry).FullName}'. An id cannot be accessed.");
            }


            var typedStore = Activator.CreateInstance(typeof(InMemoryDatabase<,>).MakeGenericType(idType, dataType));

            return (IInMemoryDatabase<TEntry>)typedStore;
        }

        #region IDatabase

        IScopedDatabase IDatabase.CreateScope()
        {
            throw new NotSupportedException();
        }

        bool IDatabase.SupportsScopes => false;

        /// <inheritdoc />
        public Task<bool> AddAsync<TEntry>(
            TEntry data,
            CancellationToken cancellation)
             where TEntry : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return GetTypedStore<TEntry>().AddAsync(data, cancellation);
        }

        /// <inheritdoc />
        public Task<bool> UpdateAsync<TEntry>(
            TEntry data,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TEntry>().StoreAsync(data, predicate, cancellation);
        }

        /// <inheritdoc />
        public Task<bool> RemoveAsync<TEntry>(
            TEntry data,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TEntry>().RemoveAsync(data, predicate, cancellation);
        }

        /// <inheritdoc />
        public Task Clear<TEntry>(CancellationToken cancellation)
            where TEntry : class
        {
            _typedStores.TryRemove(typeof(TEntry), out _);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
                    where TEntry : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TEntry>().GetAsync(predicate, cancellation);
        }

        /// <inheritdoc />
        public ValueTask<TEntry> GetOneAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation)
            where TEntry : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TEntry>().GetOneAsync(predicate, cancellation);
        }

        #endregion
    }

    internal interface IInMemoryDatabase<TEntry>
        where TEntry : class
    {
        Task<bool> AddAsync(TEntry data, CancellationToken cancellation = default);
        Task<bool> StoreAsync(TEntry data, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation);
        Task<bool> RemoveAsync(TEntry data, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation);
        IAsyncEnumerable<TEntry> GetAsync(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation);
        ValueTask<TEntry> GetOneAsync(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation);
    }

    internal sealed class InMemoryDatabase<TId, TEntry> : IInMemoryDatabase<TEntry>
        where TEntry : class
    {
        private readonly Dictionary<TId, TEntry> _entries = new Dictionary<TId, TEntry>();
        private readonly AsyncReaderWriterLock _lock = new AsyncReaderWriterLock();

        public InMemoryDatabase() { }

        public async Task<bool> StoreAsync(TEntry data, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation)
        {
            var id = DataPropertyHelper.GetId<TId, TEntry>(data);

            TEntry comparand;

            using (await _lock.ReaderLockAsync(cancellation))
            {
                if (!_entries.TryGetValue(id, out comparand))
                {
                    comparand = null;
                }
            }

            var p = predicate.Compile();

            if (comparand == null)
                return false;

            if (!p(comparand))
            {
                return false;
            }

            var copy = data.DeepClone();

            return await ExecuteAsync(data, p, (_1, _2) => _entries[id] = copy, cancellation);
        }

        public async Task<bool> AddAsync(TEntry data, CancellationToken cancellation = default)
        {
            var id = DataPropertyHelper.GetId<TId, TEntry>(data);

            TEntry comparand;

            using (await _lock.ReaderLockAsync(cancellation))
            {
                if (!_entries.TryGetValue(id, out comparand))
                {
                    comparand = null;
                }
            }

            if (comparand != null)
                return false;

            var copy = data.DeepClone();

            using (await _lock.WriterLockAsync(cancellation))
            {
                if (!_entries.TryGetValue(id, out comparand))
                {
                    comparand = null;
                }

                if (comparand != null)
                {
                    return false;
                }

                _entries[id] = copy;
            }

            return true;
        }

        public Task<bool> RemoveAsync(TEntry data, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation)
        {
            return ExecuteAsync(data, predicate.Compile(), (id, _) => _entries.Remove(id), cancellation);
        }

        private async Task<bool> ExecuteAsync(TEntry data, Func<TEntry, bool> predicate, Action<TId, TEntry> action, CancellationToken cancellation)
        {
            var id = DataPropertyHelper.GetId<TId, TEntry>(data);

            using (await _lock.WriterLockAsync(cancellation))
            {
                if (!_entries.TryGetValue(id, out var comparand))
                {
                    return false;
                }

                if (!predicate(comparand))
                {
                    return false;
                }

                action(id, data);
            }

            return true;
        }

        public IAsyncEnumerable<TEntry> GetAsync(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default)
        {
            async Task<IEnumerable<TEntry>> GetRawData()
            {
                var compiledPredicate = predicate.Compile();
                var result = new List<TEntry>();

                using (await _lock.ReaderLockAsync(cancellation))
                {
                    foreach (var entry in _entries.Values)
                    {
                        if (compiledPredicate(entry))
                            result.Add(entry);
                    }
                }

                return result;
            }

            return GetRawData().ToAsyncEnumerable().Select(p => p.DeepClone());
        }

        public async ValueTask<TEntry> GetOneAsync(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation)
        {
            var compiledPredicate = predicate.Compile();
            TEntry result = null;
            var resultFound = false;

            using (await _lock.ReaderLockAsync(cancellation))
            {
                foreach (var entry in _entries.Values)
                {
                    if (compiledPredicate(entry))
                    {
                        result = entry;
                        resultFound = true;

                        break;
                    }
                }
            }

            return resultFound ? result.DeepClone() : null;
        }
    }
}
