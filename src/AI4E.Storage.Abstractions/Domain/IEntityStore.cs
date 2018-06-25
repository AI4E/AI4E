using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public partial interface IEntityStore<TId, TEventBase, TEntityBase> : IDisposable
        where TEventBase : class
        where TEntityBase : class
    {
        ValueTask<TEntityBase> GetByIdAsync(Type entityType, TId id, CancellationToken cancellation = default);

        ValueTask<TEntityBase> GetByIdAsync(Type entityType, TId id, long revision, CancellationToken cancellation = default);

        IAsyncEnumerable<TEntityBase> GetAllAsync(Type entityType, CancellationToken cancellation = default);

        IAsyncEnumerable<TEntityBase> GetAllAsync(CancellationToken cancellation = default);

        Task StoreAsync(Type entityType, TEntityBase entity, CancellationToken cancellation = default);

        Task DeleteAsync(Type entityType, TEntityBase entity, CancellationToken cancellation = default);
    }
}
