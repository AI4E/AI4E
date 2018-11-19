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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Storage
{
    public static class DataStoreExtension
    {
        private static readonly ConditionalWeakTable<IDataStore, DataStoreExt> _extensions
            = new ConditionalWeakTable<IDataStore, DataStoreExt>();

        private sealed class DataStoreExt
        {
            private static readonly MethodInfo _storeMethodDefinition;
            private static readonly MethodInfo _removeMethodDefinition;

            private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, Task>> _storeMethods;
            private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, Task>> _removeMethods;
            private volatile IDataStore _dataStore;

            static DataStoreExt()
            {
                _storeMethodDefinition = typeof(IDataStore).GetMethods().Single(p => p.Name == nameof(IDataStore.StoreAsync));
                _removeMethodDefinition = typeof(IDataStore).GetMethods().Single(p => p.Name == nameof(IDataStore.RemoveAsync));
            }

            public DataStoreExt()
            {
                _storeMethods = new ConcurrentDictionary<Type, Func<object, CancellationToken, Task>>();
                _removeMethods = new ConcurrentDictionary<Type, Func<object, CancellationToken, Task>>();
            }

            public void Initialize(IDataStore dataStore)
            {
                // Volatile read op
                if (_dataStore != null)
                {
                    return;
                }

                // Write _dataStore only if there is no data store present currently.
                var current = Interlocked.CompareExchange(ref _dataStore, dataStore, null);

                // If there was a data store present, it must be the same than the one, we wanted to write.
                Assert(current == null || ReferenceEquals(current, dataStore));
            }

            public Task StoreAsync(Type dataType, object data, CancellationToken cancellation)
            {
                Assert(data != null);

                var function = _storeMethods.GetOrAdd(dataType, BuildStoreMethod);

                Assert(function != null);

                return function(data, cancellation);
            }

            public Task RemoveAsync(Type dataType, object data, CancellationToken cancellation)
            {
                Assert(data != null);

                var function = _removeMethods.GetOrAdd(dataType, BuildStoreMethod);

                Assert(function != null);

                return function(data, cancellation);
            }

            private Func<object, CancellationToken, Task> BuildStoreMethod(Type type)
            {
                Assert(type != null);
                var methodInfo = _storeMethodDefinition.MakeGenericMethod(type);
                return BuildMethod(type, methodInfo);
            }

            private Func<object, CancellationToken, Task> BuildRemoveMethod(Type type)
            {
                Assert(type != null);
                var methodInfo = _removeMethodDefinition.MakeGenericMethod(type);
                return BuildMethod(type, methodInfo);
            }

            private Func<object, CancellationToken, Task> BuildMethod(Type type, MethodInfo method)
            {
                Assert(type != null);
                Assert(method != null);

                // Volatile read op
                var dataStore = _dataStore;

                var instance = Expression.Constant(dataStore, typeof(IDataStore));

                var dataParam = Expression.Parameter(typeof(object), "data");
                var cancellationParam = Expression.Parameter(typeof(CancellationToken), "cancellation");
                var data = Expression.Convert(dataParam, type);

                var call = Expression.Call(instance, method, data, cancellationParam);
                return Expression.Lambda<Func<object, CancellationToken, Task>>(call, dataParam, cancellationParam).Compile();
            }
        }

        private static DataStoreExt GetExtension(IDataStore dataStore)
        {
            Assert(dataStore != null);

            var result = _extensions.GetOrCreateValue(dataStore);
            Assert(result != null);

            result.Initialize(dataStore);

            return result;
        }

        /// <summary>
        /// Stores an object in the store.
        /// </summary>
        /// <param name="dataStore">The data store.</param>
        /// <param name="data">The object to update.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="dataStore"/> or <paramref name="data"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        public static Task StoreAsync(this IDataStore dataStore, object data, CancellationToken cancellation = default)
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data is ValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(data));

            return GetExtension(dataStore).StoreAsync(data.GetType(), data, cancellation);
        }

        /// <summary>
        /// Removes an object from the store.
        /// </summary>
        /// <param name="dataStore">The data store.</param>
        /// <param name="data">The object to remove.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="dataStore"/> or <paramref name="data"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        public static Task RemoveAsync(this IDataStore dataStore, object data, CancellationToken cancellation = default)
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data is ValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(data));

            return GetExtension(dataStore).RemoveAsync(data.GetType(), data, cancellation);
        }

        public static Task StoreAsync(this IDataStore dataStore, Type dataType, object data, CancellationToken cancellation = default)
        {
            CheckArguments(dataStore, dataType, data);
            return GetExtension(dataStore).StoreAsync(dataType, data, cancellation);
        }

        public static Task RemoveAsync(this IDataStore dataStore, Type dataType, object data, CancellationToken cancellation = default)
        {
            CheckArguments(dataStore, dataType, data);
            return GetExtension(dataStore).RemoveAsync(dataType, data, cancellation);
        }

        private static void CheckArguments(IDataStore dataStore, Type dataType, object data)
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (dataType.IsValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(dataType));

            if (!dataType.IsAssignableFrom(data.GetType()))
                throw new ArgumentException($"The specified data must be of type '{dataType.FullName}' or an assignable type.");
        }

        public static IAsyncEnumerable<TData> QueryAsync<TData>(this IQueryableDataStore dataStore, Func<IQueryable<TData>, IQueryable<TData>> queryShaper, CancellationToken cancellation = default)
            where TData : class
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (queryShaper == null)
                throw new ArgumentNullException(nameof(queryShaper));

            return dataStore.QueryAsync(queryShaper, cancellation);
        }
    }
}
