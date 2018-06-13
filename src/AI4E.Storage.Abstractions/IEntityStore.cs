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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    //public interface IEntityStore<TId, TEventBase, TEntityBase> : IDisposable
    //    where TId : struct, IEquatable<TId>
    //    where TEventBase : class
    //    where TEntityBase : class
    //{
    //    Task<TEntity> GetByIdAsync<TEntity>(TId id, CancellationToken cancellation = default)
    //        where TEntity : class, TEntityBase;

    //    Task<TEntity> GetByIdAsync<TEntity>(TId id, long revision, CancellationToken cancellation = default)
    //        where TEntity : class, TEntityBase;

    //    Task<TEntityBase> GetByIdAsync(Type entityType, TId id, CancellationToken cancellation = default);

    //    Task<TEntityBase> GetByIdAsync(Type entityType, TId id, long revision, CancellationToken cancellation = default);

    //    Task<IEnumerable<TEntity>> GetAllAsync<TEntity>(CancellationToken cancellation = default)
    //        where TEntity : class, TEntityBase;

    //    IAsyncEnumerable<TEntityBase> GetAllAsync(Type entityType, CancellationToken cancellation = default);

    //    IAsyncEnumerable<TEntityBase> GetAllAsync(CancellationToken cancellation = default);

    //    Task StoreAsync<TEntity>(TEntity entity, CancellationToken cancellation = default)
    //        where TEntity : class, TEntityBase;

    //    Task StoreAsync(Type entityType, TEntityBase entity, CancellationToken cancellation = default);

    //    Task DeleteAsync<TEntity>(TEntity entity, CancellationToken cancellation = default)
    //        where TEntity : class, TEntityBase;

    //    Task DeleteAsync(Type entityType, TEntityBase entity, CancellationToken cancellation = default);

    //    IEnumerable<(Type type, TId id, long revision, TEntityBase entity)> CachedEntries { get; }
    //}

    public interface IEntityStore : IDisposable
    {
        ValueTask<object> GetByIdAsync(Type entityType, string id, CancellationToken cancellation = default);

        ValueTask<object> GetByIdAsync(Type entityType, string id, long revision, CancellationToken cancellation = default);

        IAsyncEnumerable<object> GetAllAsync(Type entityType, CancellationToken cancellation = default);

        IAsyncEnumerable<object> GetAllAsync(CancellationToken cancellation = default);

        Task StoreAsync(Type entityType, object entity, CancellationToken cancellation = default);

        Task DeleteAsync(Type entityType, object entity, CancellationToken cancellation = default);

        //IEnumerable<(Type type, string id, long revision, object entity)> CachedEntries { get; }
    }
}
