using System;

namespace AI4E.Storage.Domain
{
    public interface IEntityIdManager
    {
        bool TryGetId(Type entityType, object entity, out string id);
        bool TrySetId(Type entityType, object entity, string id);
    }

    public static class EntityIdManagerExtension
    {
        public static bool TryGetId<TEntity>(this IEntityIdManager idManager, TEntity entity, out string id)
        {
            if (idManager == null)
                throw new ArgumentNullException(nameof(idManager));

            return idManager.TryGetId(typeof(TEntity), entity, out id);
        }

        public static bool TrySetId<TEntity>(this IEntityIdManager idManager, TEntity entity, string id)
        {
            if (idManager == null)
                throw new ArgumentNullException(nameof(idManager));

            return idManager.TrySetId(typeof(TEntity), entity, id);
        }
    }
}
