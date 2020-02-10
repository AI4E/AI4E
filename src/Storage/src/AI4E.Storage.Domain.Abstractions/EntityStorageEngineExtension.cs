using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1062

namespace AI4E.Storage.Domain
{
    public static class EntityStorageEngineExtension
    {
        public static async Task StoreAsync(
            this IEntityStorageEngine storageEngine,
            Type entityType,
            object entity,
            CancellationToken cancellation = default)
        {
            if (!await storageEngine.TryStoreAsync(entityType, entity, cancellation).ConfigureAwait(false))
            {
                throw new ConcurrencyException();
            }
        }

        public static async Task StoreAsync(
            this IEntityStorageEngine storageEngine,
            Type entityType,
            object entity,
            string id,
            CancellationToken cancellation = default)
        {
            if (!await storageEngine.TryStoreAsync(entityType, entity, id, cancellation).ConfigureAwait(false))
            {
                throw new ConcurrencyException();
            }
        }

        public static async Task DeleteAsync(
            this IEntityStorageEngine storageEngine,
            Type entityType,
            object entity,
            CancellationToken cancellation = default)
        {
            if (!await storageEngine.TryDeleteAsync(entityType, entity, cancellation).ConfigureAwait(false))
            {
                throw new ConcurrencyException();
            }
        }

        public static async Task DeleteAsync(
            this IEntityStorageEngine storageEngine,
            Type entityType,
            object entity,
            string id,
            CancellationToken cancellation = default)
        {
            if (!await storageEngine.TryDeleteAsync(entityType, entity, id, cancellation).ConfigureAwait(false))
            {
                throw new ConcurrencyException();
            }
        }

        public static async ValueTask<TEntity?> GetByIdAsync<TEntity>(
            this IEntityStorageEngine storageEngine,
            string id,
            CancellationToken cancellation = default)
            where TEntity : class
        {
            return (await storageEngine.GetByIdAsync(typeof(TEntity), id, cancellation)) as TEntity;
        }

        public static async ValueTask<TEntity?> GetByIdAsync<TEntity>(
            this IEntityStorageEngine storageEngine,
            string id,
            long revision,
            CancellationToken cancellation = default)
            where TEntity : class
        {
            return (await storageEngine.GetByIdAsync(typeof(TEntity), id, revision, cancellation)) as TEntity;
        }

        public static IAsyncEnumerable<TEntity> GetAllAsync<TEntity>(
            this IEntityStorageEngine storageEngine,
            CancellationToken cancellation = default)
            where TEntity : class
        {
            return storageEngine.GetAllAsync(typeof(TEntity), cancellation).Cast<TEntity>();
        }

        public static Task StoreAsync<TEntity>(
            this IEntityStorageEngine storageEngine,
            TEntity entity,
            CancellationToken cancellation = default)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.StoreAsync(typeof(TEntity), entity, cancellation);
        }

        public static Task StoreAsync<TEntity>(
            this IEntityStorageEngine storageEngine,
            TEntity entity,
            string id,
            CancellationToken cancellation = default)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.StoreAsync(typeof(TEntity), entity, id, cancellation);
        }

        public static Task DeleteAsync<TEntity>(
            this IEntityStorageEngine storageEngine,
            TEntity entity,
            CancellationToken cancellation = default)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.DeleteAsync(typeof(TEntity), entity, cancellation);
        }

        public static Task DeleteAsync<TEntity>(
            this IEntityStorageEngine storageEngine,
            TEntity entity,
            string id,
            CancellationToken cancellation = default)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.DeleteAsync(typeof(TEntity), entity, id, cancellation);
        }

        public static Task<bool> TryStoreAsync<TEntity>(this IEntityStorageEngine storageEngine, TEntity entity, CancellationToken cancellation = default)
            where TEntity : class
        {
            return storageEngine.TryStoreAsync(typeof(TEntity), entity, cancellation);
        }

        public static Task<bool> TryDeleteAsync<TEntity>(this IEntityStorageEngine storageEngine, TEntity entity, CancellationToken cancellation = default)
           where TEntity : class
        {
            return storageEngine.TryDeleteAsync(typeof(TEntity), entity, cancellation);
        }

        public static ValueTask<object> GetByIdAsync(
            this IEntityStorageEngine storageEngine,
            Type entityType,
            string id,
            CancellationToken cancellation = default)
        {
            return storageEngine.GetByIdAsync(entityType, id, bypassCache: false, cancellation);
        }

        public static ValueTask<long> GetRevisionAsync(
            this IEntityStorageEngine storageEngine,
            Type entityType,
            string id,
            CancellationToken cancellation = default)
        {
            return storageEngine.GetRevisionAsync(entityType, id, bypassCache: false, cancellation);
        }
    }
}
#pragma warning restore CA1062