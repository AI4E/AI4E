using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static AI4E.Internal.DebugEx;

namespace AI4E.Storage.Domain
{
    public sealed partial class EntityStore<TId, TEventBase, TEntityBase>
    {
        public async ValueTask<TEntity> GetByIdAsync<TEntity>(TId id, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase
        {
            var result = await GetByIdAsync(typeof(TEntity), id, cancellation);

            Assert(result != null, result is TEntity);

            return result as TEntity;
        }

        public async ValueTask<TEntity> GetByIdAsync<TEntity>(TId id, long revision, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase
        {
            var result = await GetByIdAsync(typeof(TEntity), id, revision, cancellation);

            Assert(result != null, result is TEntity);

            return result as TEntity;
        }

        public IAsyncEnumerable<TEntity> GetAllAsync<TEntity>(CancellationToken cancellation = default)
            where TEntity : class, TEntityBase
        {
            return GetAllAsync(typeof(TEntity), cancellation).AssertEach(p => p is TEntityBase).Cast<TEntity>();
        }

        public Task StoreAsync<TEntity>(TEntity entity, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase
        {
            return StoreAsync(typeof(TEntity), entity, cancellation);
        }

        public Task DeleteAsync<TEntity>(TEntity entity, CancellationToken cancellation = default)
            where TEntity : class, TEntityBase
        {
            return DeleteAsync(typeof(TEntity), entity, cancellation);
        }
    }
}
