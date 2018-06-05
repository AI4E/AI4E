﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Storage.InMemory.Internal;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.InMemory
{
    public sealed class InMemoryDatabase : IFilterableDatabase
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

            var typedStore = Activator.CreateInstance(typeof(InMemoryDatabase<,>).MakeGenericType(idType, dataType));

            return (IInMemoryDatabase<TData>)typedStore;
        }

        #region IDatabase

        public ValueTask<bool> AddAsync<TData>(TData data, CancellationToken cancellation = default)
             where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return GetTypedStore<TData>().StoreAsync(data, p => p == null, cancellation);
        }

        public ValueTask<bool> UpdateAsync<TData>(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TData>().StoreAsync(data, predicate, cancellation);
        }

        public ValueTask<bool> RemoveAsync<TData>(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
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
        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(CancellationToken cancellation = default) where TEntry : class
        {
            return GetAsync<TEntry>(p => true, cancellation);
        }

        public ValueTask<TEntry> GetOneAsync<TId, TEntry>(TId id, CancellationToken cancellation = default)
            where TId : IEquatable<TId>
            where TEntry : class
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            return GetTypedStore<TEntry>().GetOneAsync(DataPropertyHelper.BuildPredicate<TId, TEntry>(id), cancellation);
        }

        public ValueTask<TEntry> GetOneAsync<TEntry>(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TEntry>().GetOneAsync(predicate, cancellation);
        }

        public ValueTask<bool> CompareExchangeAsync<TEntry>(TEntry entry, TEntry comparand, Expression<Func<TEntry, TEntry, bool>> equalityComparer, CancellationToken cancellation = default) where TEntry : class
        {
            if (equalityComparer == null)
                throw new ArgumentNullException(nameof(equalityComparer));

            // This is a nop actually. But we check whether comparand is up to date.
            if (entry == comparand)
            {
                return CheckComparandToBeUpToDate(comparand, equalityComparer, cancellation);
            }

            // Trying to update an entry.
            if (entry != null && comparand != null)
            {
                return UpdateAsync(entry, BuildPredicate(comparand, equalityComparer), cancellation);
            }

            // Trying to create an entry.
            if (entry != null)
            {
                return AddAsync(entry, cancellation);
            }

            // Trying to remove an entry.
            Assert(comparand != null);

            return RemoveAsync(comparand, BuildPredicate(comparand, equalityComparer), cancellation);
        }

        public async ValueTask<TEntry> GetOrAdd<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            while (!await AddAsync(entry, cancellation))
            {
                var result = await GetOneAsync(DataPropertyHelper.BuildPredicate(entry), cancellation);

                if (result != null)
                    return result;
            }

            return entry;
        }

        private async ValueTask<bool> CheckComparandToBeUpToDate<TEntry>(TEntry comparand,
                                                                         Expression<Func<TEntry, TEntry, bool>> equalityComparer,
                                                                         CancellationToken cancellation)
            where TEntry : class
        {
            var result = await GetOneAsync(DataPropertyHelper.BuildPredicate(comparand), cancellation);

            if (comparand == null)
            {
                return result == null;
            }

            if (result == null)
                return false;

            return equalityComparer.Compile(preferInterpretation: true).Invoke(comparand, result);
        }

        private static Expression<Func<TEntry, bool>> BuildPredicate<TEntry>(TEntry comparand,
                                                                             Expression<Func<TEntry, TEntry, bool>> equalityComparer)
        {
            Assert(comparand != null);
            Assert(equalityComparer != null);

            var idSelector = DataPropertyHelper.BuildPredicate(comparand);
            var comparandConstant = Expression.Constant(comparand, typeof(TEntry));
            var parameter = equalityComparer.Parameters.First();
            var equality = ParameterExpressionReplacer.ReplaceParameter(equalityComparer.Body, equalityComparer.Parameters.Last(), comparandConstant);
            var idEquality = ParameterExpressionReplacer.ReplaceParameter(idSelector.Body, idSelector.Parameters.First(), parameter);
            var body = Expression.AndAlso(idEquality, equality);

            return Expression.Lambda<Func<TEntry, bool>>(body, parameter);
        }

        #endregion
    }

    internal interface IInMemoryDatabase<TData>
        where TData : class
    {
        ValueTask<bool> StoreAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        ValueTask<bool> RemoveAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        IAsyncEnumerable<TData> GetAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        ValueTask<TData> GetOneAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
    }

    internal sealed class InMemoryDatabase<TId, TData> : IInMemoryDatabase<TData>
        where TData : class
    {
        private readonly Dictionary<TId, TData> _entries = new Dictionary<TId, TData>();
        private readonly AsyncReaderWriterLock _lock = new AsyncReaderWriterLock();

        public InMemoryDatabase() { }

        public async ValueTask<bool> StoreAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
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

            if (!p(comparand))
            {
                return false;
            }

            var copy = data.DeepCopyByExpressionTree();

            return await ExecuteAsync(data, p, (_1, _2) => _entries[id] = copy, cancellation);
        }

        public ValueTask<bool> RemoveAsync(TData data, Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
        {
            return ExecuteAsync(data, predicate.Compile(), (id, _) => _entries.Remove(id), cancellation);
        }

        private async ValueTask<bool> ExecuteAsync(TData data, Func<TData, bool> predicate, Action<TId, TData> action, CancellationToken cancellation)
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

            return GetRawData().ToAsyncEnumerable().Select(p => p.DeepCopyByExpressionTree());
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

            return resultFound ? result.DeepCopyByExpressionTree() : null;
        }
    }
}