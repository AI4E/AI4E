using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    public interface IEntityPropertyAccessor
    {
        bool TryGetId(Type entityType, object entity, [NotNullWhen(true)] out string? id);
        bool TrySetId(Type entityType, object entity, string id);

        string GetConcurrencyToken(Type entityType, object entity);
        void SetConcurrencyToken(Type entityType, object entity, string concurrencyToken);

        long GetRevision(Type entityType, object entity);
        void SetRevision(Type entityType, object entity, long revision);

        void CommitEvents(Type entityType, object entity);
        IEnumerable<object> GetUncommittedEvents(Type entityType, object entity);
        bool TryAddEvent(Type entityType, object entity, object evt);
    }

    public static class EntityPropertyAccessorExtension
    {
        public static bool TryGetId<TEntity>(
            this IEntityPropertyAccessor entityPropertyAccessor, 
            TEntity entity, 
            [NotNullWhen(true)] out string? id)
            where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            return entityPropertyAccessor.TryGetId(typeof(TEntity), entity, out id);
        }

        public static bool TrySetId<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, TEntity entity, string id)
            where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            return entityPropertyAccessor.TrySetId(typeof(TEntity), entity, id);
        }

        public static string GetConcurrencyToken<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, object entity)
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            return entityPropertyAccessor.GetConcurrencyToken(typeof(TEntity), entity);
        }

        public static void SetConcurrencyToken<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, TEntity entity, string concurrencyToken)
            where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            entityPropertyAccessor.SetConcurrencyToken(typeof(TEntity), entity, concurrencyToken);
        }

        public static long GetRevision<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, TEntity entity)
            where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            return entityPropertyAccessor.GetRevision(typeof(TEntity), entity);
        }

        public static void SetRevision<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, TEntity entity, long revision)
            where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            entityPropertyAccessor.SetRevision(typeof(TEntity), entity, revision);
        }

        public static void CommitEvents<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, TEntity entity)
            where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            entityPropertyAccessor.CommitEvents(typeof(TEntity), entity);
        }

        public static IEnumerable<object> GetUncommittedEvents<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, TEntity entity)
            where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            return entityPropertyAccessor.GetUncommittedEvents(typeof(TEntity), entity);
        }

        public static bool TryAddEvent<TEntity>(this IEntityPropertyAccessor entityPropertyAccessor, TEntity entity, object evt)
           where TEntity : class
        {
            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            return entityPropertyAccessor.TryAddEvent(typeof(TEntity), entity, evt);
        }
    }
}
