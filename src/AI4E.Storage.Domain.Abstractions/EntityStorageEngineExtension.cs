using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public static class EntityStorageEngineExtension
    {
        public static async Task StoreAsync(this IEntityStorageEngine storageEngine, Type entityType, object entity, CancellationToken cancellation = default)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (!await storageEngine.TryStoreAsync(entityType, entity, cancellation))
            {
                throw new ConcurrencyException();
            }
        }

        public static async Task StoreAsync(this IEntityStorageEngine storageEngine, Type entityType, object entity, string id, CancellationToken cancellation = default)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (!await storageEngine.TryStoreAsync(entityType, entity, id, cancellation))
            {
                throw new ConcurrencyException();
            }
        }

        public static async Task DeleteAsync(this IEntityStorageEngine storageEngine, Type entityType, object entity, CancellationToken cancellation = default)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (!await storageEngine.TryDeleteAsync(entityType, entity, cancellation))
            {
                throw new ConcurrencyException();
            }
        }

        public static async Task DeleteAsync(this IEntityStorageEngine storageEngine, Type entityType, object entity, string id, CancellationToken cancellation = default)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (!await storageEngine.TryDeleteAsync(entityType, entity, id, cancellation))
            {
                throw new ConcurrencyException();
            }
        }

        public static async ValueTask<TEntity> GetByIdAsync<TEntity>(this IEntityStorageEngine storageEngine, string id, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            return (await storageEngine.GetByIdAsync(typeof(TEntity), id, cancellation)) as TEntity;
        }

        public static async ValueTask<TEntity> GetByIdAsync<TEntity>(this IEntityStorageEngine storageEngine, string id, long revision, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            return (await storageEngine.GetByIdAsync(typeof(TEntity), id, revision, cancellation)) as TEntity;
        }

        public static async ValueTask<(TEntity entity, long revision)> LoadEntityAsync<TEntity>(this IEntityStorageEngine storageEngine, string id, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            var (entity, revision) = await storageEngine.LoadEntityAsync(typeof(TEntity), id, cancellation);

            if (!(entity is TEntity convertedEntity))
            {
                return (null, default);
            }

            return (convertedEntity, revision);
        }

        public static IAsyncEnumerable<TEntity> GetAllAsync<TEntity>(this IEntityStorageEngine storageEngine, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            return storageEngine.GetAllAsync(typeof(TEntity), cancellation).Cast<TEntity>();
        }

        public static Task StoreAsync<TEntity>(this IEntityStorageEngine storageEngine, TEntity entity, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.StoreAsync(typeof(TEntity), entity, cancellation);
        }

        public static Task StoreAsync<TEntity>(this IEntityStorageEngine storageEngine, TEntity entity, string id, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.StoreAsync(typeof(TEntity), entity, id, cancellation);
        }

        public static Task DeleteAsync<TEntity>(this IEntityStorageEngine storageEngine, TEntity entity, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.DeleteAsync(typeof(TEntity), entity, cancellation);
        }

        public static Task DeleteAsync<TEntity>(this IEntityStorageEngine storageEngine, TEntity entity, string id, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

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
    }
}
