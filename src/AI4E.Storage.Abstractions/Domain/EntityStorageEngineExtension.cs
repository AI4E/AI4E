using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public static class EntityStorageEngineExtension
    {
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

        public static Task StoreAsync<TEntity>(this IEntityStorageEngine storageEngine, TEntity entity, string id, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return storageEngine.StoreAsync(typeof(TEntity), entity, id, cancellation);
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
    }
}
