using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using AI4E.Utils.AsyncEnumerable;
using Nito.AsyncEx;

namespace AI4E.Storage.InMemory
{
    public sealed class InMemoryDatabase : IDatabase
    {
        private readonly ConcurrentDictionary<Type, object> _typedStores = new ConcurrentDictionary<Type, object>();

        private IInMemoryDatabase<TData> GetTypedStore<TData>()
            where TData : class
        {
            return (IInMemoryDatabase<TData>)_typedStores.GetOrAdd(typeof(TData), _ => CreateTypedStore<TData>());
        }

        private IInMemoryDatabase<TData> CreateTypedStore<TData>()
            where TData : class
        {
            var dataType = typeof(TData);
            var idType = DataPropertyHelper.GetIdType<TData>();

            if (idType == null)
            {
                throw new Exception($"Cannot store objects of type '{typeof(TData).FullName}'. An id cannot be extracted."); // TODO
            }


            var typedStore = Activator.CreateInstance(typeof(InMemoryDatabase<,>).MakeGenericType(idType, dataType));

            return (IInMemoryDatabase<TData>)typedStore;
        }

        #region IDatabase

        IScopedDatabase IDatabase.CreateScope()
        {
            throw new NotSupportedException();
        }

        bool IDatabase.SupportsScopes => false;

        public Task<bool> AddAsync<TData>(TData data, CancellationToken cancellation = default)
             where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return GetTypedStore<TData>().AddAsync(data, cancellation);
        }

        public Task<bool> UpdateAsync<TData>(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TData>().StoreAsync(data, predicate, cancellation);
        }

        public Task<bool> RemoveAsync<TData>(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TData>().RemoveAsync(data, predicate, cancellation);
        }

        public Task Clear<TEntry>(CancellationToken cancellation = default) where TEntry : class
        {
            _typedStores.TryRemove(typeof(TEntry), out _);

            return Task.CompletedTask;
        }

        public IAsyncEnumerable<TData> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
                    where TData : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TData>().GetAsync(predicate, cancellation);
        }

        public ValueTask<TEntry> GetOneAsync<TEntry>(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TEntry>().GetOneAsync(predicate, cancellation);
        }

        #endregion
    }

    internal interface IInMemoryDatabase<TData>
        where TData : class
    {
        Task<bool> AddAsync(TData data, CancellationToken cancellation = default);
        Task<bool> StoreAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        Task<bool> RemoveAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        IAsyncEnumerable<TData> GetAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        ValueTask<TData> GetOneAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
    }

    internal sealed class InMemoryDatabase<TId, TData> : IInMemoryDatabase<TData>
        where TData : class
    {
        private readonly Dictionary<TId, TData> _entries = new Dictionary<TId, TData>();
        private readonly AsyncReaderWriterLock _lock = new AsyncReaderWriterLock();

        public InMemoryDatabase() { }

        public async Task<bool> StoreAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
        {
            var id = DataPropertyHelper.GetId<TId, TData>(data);

            TData comparand;

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

        public async Task<bool> AddAsync(TData data, CancellationToken cancellation = default)
        {
            var id = DataPropertyHelper.GetId<TId, TData>(data);

            TData comparand;

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

        public Task<bool> RemoveAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
        {
            return ExecuteAsync(data, predicate.Compile(), (id, _) => _entries.Remove(id), cancellation);
        }

        private async Task<bool> ExecuteAsync(TData data, Func<TData, bool> predicate, Action<TId, TData> action, CancellationToken cancellation)
        {
            var id = DataPropertyHelper.GetId<TId, TData>(data);

            using (await _lock.WriterLockAsync(cancellation))
            {
                if (!_entries.TryGetValue(id, out var comparand))
                {
                    comparand = null;
                }

                if (!predicate(comparand))
                {
                    return false;
                }

                action(id, data);
            }

            return true;
        }

        public IAsyncEnumerable<TData> GetAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
        {
            async Task<IEnumerable<TData>> GetRawData()
            {
                var compiledPredicate = predicate.Compile();
                var result = new List<TData>();

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

        public async ValueTask<TData> GetOneAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
        {
            var compiledPredicate = predicate.Compile();
            TData result = null;
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
