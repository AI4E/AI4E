using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    // These overloads could be extension methods but as the extended type is generic, and the extension methods does need
    // additional generic parameters, this would need the caller to specify all generic arguments while
    // the compiler could (theoretically) determine the generic arguments used for the extended type by the provided instance.
    public partial interface IEntityStore<TId, TEventBase, TEntityBase>
    {
        ValueTask<TEntity> GetByIdAsync<TEntity>(TId id, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase;

        ValueTask<TEntity> GetByIdAsync<TEntity>(TId id, long revision, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase;

        IAsyncEnumerable<TEntity> GetAllAsync<TEntity>(CancellationToken cancellation = default)
            where TEntity : class, TEntityBase;

        Task StoreAsync<TEntity>(TEntity entity, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase;

        Task DeleteAsync<TEntity>(TEntity entity, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase;
    }
}
