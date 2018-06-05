using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.InMemory.Internal;
using Nito.AsyncEx;

namespace AI4E.Storage.InMemory
{
    [Obsolete]
    public sealed class InMemoryDataStore : IDataStore
    {
        private readonly ConcurrentDictionary<Type, object> _typeStores = new ConcurrentDictionary<Type, object>();

        private IInMemoryDataStore<TData> GetTypedStore<TData>()
        {
            return (IInMemoryDataStore<TData>)_typeStores.GetOrAdd(typeof(TData), _ => CreateTypedStore<TData>());
        }

        private IInMemoryDataStore<TData> CreateTypedStore<TData>()
        {
            var idProperty = typeof(TData).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (idProperty == null || !idProperty.CanRead || idProperty.GetIndexParameters().Length != 0)
            {
                throw new InvalidOperationException("The type must declare an 'Id' property can can be read and does not specify any index parameters.");
            }

            var idType = idProperty.PropertyType;

            if (!idType.IsValueType || !idType.GetInterfaces().Any(p => p.IsGenericType &&
                                                                        p.GetGenericTypeDefinition() == typeof(IEquatable<>) &&
                                                                        p.GetGenericArguments().SequenceEqual(Enumerable.Repeat(idType, 1))))
            {
                throw new InvalidOperationException("The id must be a value type that imlements 'IEquatable<T>'.");
            }

            var idAccessor = Activator.CreateInstance(typeof(IdAccessor<,>).MakeGenericType(idType, typeof(TData)), idProperty);
            var typedStore = Activator.CreateInstance(typeof(InMemoryDataStore<,>).MakeGenericType(idType, typeof(TData)), idAccessor);

            return (IInMemoryDataStore<TData>)typedStore;
        }

        public Task Clear(CancellationToken cancellation = default)
        {
            _typeStores.Clear();
            return Task.CompletedTask;
        }

        public Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            return GetTypedStore<TData>().StoreAsync(data, cancellation);
        }

        public Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            return GetTypedStore<TData>().RemoveAsync(data, cancellation);
        }

        public Task<IEnumerable<TResult>> QueryAsync<TData, TResult>(Func<IQueryable<TData>, IQueryable<TResult>> queryShaper, CancellationToken cancellation = default)
            where TData : class
        {
            return GetTypedStore<TData>().QueryAsync(queryShaper, cancellation);
        }
    }

    internal interface IInMemoryDataStore<TData>
    {
        Task StoreAsync(TData data, CancellationToken cancellation = default);
        Task RemoveAsync(TData data, CancellationToken cancellation = default);
        Task<IEnumerable<TResult>> QueryAsync<TResult>(Func<IQueryable<TData>, IQueryable<TResult>> queryShaper, CancellationToken cancellation = default);
    }

    // This is a (way too) simple implementation. There are no secondary indices currently.
    internal sealed class InMemoryDataStore<TId, TData> : IInMemoryDataStore<TData>
        where TId : struct, IEquatable<TId>
        where TData : class
    {
        private readonly Dictionary<TId, TData> _values = new Dictionary<TId, TData>();
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly IIdAccessor<TId, TData> _idAccessor;

        public InMemoryDataStore(IIdAccessor<TId, TData> idAccessor)
        {
            if (idAccessor == null)
                throw new ArgumentNullException(nameof(idAccessor));
            _idAccessor = idAccessor;
        }

        public async Task StoreAsync(TData data, CancellationToken cancellation = default)
        {
            var id = _idAccessor.GetId(data);
            var copy = data.Copy();

            using (await _lock.LockAsync(cancellation))
            {
                _values[id] = data;
            }
        }

        public async Task RemoveAsync(TData data, CancellationToken cancellation = default)
        {
            var id = _idAccessor.GetId(data);

            using (await _lock.LockAsync(cancellation))
            {
                _values.Remove(id);
            }
        }

        public async Task<IEnumerable<TResult>> QueryAsync<TResult>(Func<IQueryable<TData>, IQueryable<TResult>> queryShaper, CancellationToken cancellation = default)
        {
            var queryable = default(IQueryable<TData>);

            using (await _lock.LockAsync(cancellation))
            {
                queryable = _values.Values.ToList().AsQueryable(); // TODO: This does not scale
            }

            var shapedQueryable = (IEnumerable<TResult>)queryShaper(queryable);

            shapedQueryable = shapedQueryable.Select(p => p.Copy());

            return shapedQueryable;
        }
    }

    public interface IIdAccessor<TId, TData>
        where TId : struct, IEquatable<TId>
        where TData : class
    {
        TId GetId(TData data);
    }

    internal sealed class IdAccessor<TId, TData> : IIdAccessor<TId, TData>
        where TId : struct, IEquatable<TId>
        where TData : class
    {
        private readonly Func<TData, TId> _invoker;

        public IdAccessor(PropertyInfo property)
        {
            var dataParameter = Expression.Parameter(typeof(TData), "data");
            var propertyAccess = Expression.Property(dataParameter, property);
            _invoker = Expression.Lambda<Func<TData, TId>>(propertyAccess, dataParameter).Compile();
        }

        public TId GetId(TData data)
        {
            return _invoker(data);
        }
    }
}
