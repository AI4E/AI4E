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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public static class DataStoreExtension
    {
        private static readonly MethodInfo _storeMethodDefinition;
        private static readonly MethodInfo _removeMethodDefinition;
        private static readonly ConcurrentDictionary<Type, MethodInfo> _storeMethods = new ConcurrentDictionary<Type, MethodInfo>();
        private static readonly ConcurrentDictionary<Type, MethodInfo> _removeMethods = new ConcurrentDictionary<Type, MethodInfo>();

        static DataStoreExtension()
        {
            _storeMethodDefinition = typeof(IDataStore).GetMethods().Single(p => p.Name == nameof(IDataStore.StoreAsync));
            _removeMethodDefinition = typeof(IDataStore).GetMethods().Single(p => p.Name == nameof(IDataStore.RemoveAsync));
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

            return (Task)_storeMethods.GetOrAdd(data.GetType(), type => _storeMethodDefinition.MakeGenericMethod(type)).Invoke(dataStore, new object[] { data, cancellation });
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

            return (Task)_removeMethods.GetOrAdd(data.GetType(), type => _removeMethodDefinition.MakeGenericMethod(type)).Invoke(dataStore, new object[] { data, cancellation });
        }

        public static Task<IEnumerable<TData>> QueryAsync<TData>(this IDataStore dataStore, Func<IQueryable<TData>, IQueryable<TData>> queryShaper, CancellationToken cancellation = default)
            where TData : class
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (queryShaper == null)
                throw new ArgumentNullException(nameof(queryShaper));

            return dataStore.QueryAsync(queryShaper, cancellation);
        }

        public static Task<IEnumerable<TData>> FindAsync<TData>(this IDataStore dataStore, Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
             where TData : class
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return dataStore.QueryAsync<TData, TData>(queryable => queryable.Where(predicate), cancellation);
        }

        public static async Task<TData> FindOneAsync<TData>(this IDataStore dataStore, Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
             where TData : class
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return (await dataStore.QueryAsync<TData, TData>(queryable => queryable.Where(predicate).Take(1), cancellation)).FirstOrDefault();
        }

        public static Task<IEnumerable<TData>> FindAsync<TData>(this IDataStore dataStore, CancellationToken cancellation = default)
             where TData : class
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            return dataStore.QueryAsync<TData, TData>(queryable => queryable, cancellation);
        }

        public static async Task<TData> FindOneAsync<TData>(this IDataStore dataStore, CancellationToken cancellation = default)
 where TData : class
        {
            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            return (await dataStore.QueryAsync<TData, TData>(queryable => queryable.Take(1), cancellation)).FirstOrDefault();
        }
    }
}
