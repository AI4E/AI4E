using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace AI4E.Storage
{
    public sealed class DefaultEntityAccessor<TId, TEventBase, TEntityBase> : IEntityAccessor<TId, TEventBase, TEntityBase>
        where TId : struct, IEquatable<TId>
        where TEventBase : class
        where TEntityBase : class
    {
        private readonly Func<TEntityBase, TId> _idAccessor;
        private readonly Func<TEntityBase, Guid> _concurrencyTokenAccessor;
        private readonly Action<TEntityBase, Guid> _concurrencyTokenSetter;
        private readonly Func<TEntityBase, long> _revisionAccessor;
        private readonly Action<TEntityBase, long> _revisionSetter;
        private readonly Func<TEntityBase, IEnumerable<TEventBase>> _uncommittedEventsAccessor;
        private readonly Action<TEntityBase> _commitMethodInvoker;

        public DefaultEntityAccessor()
        {
            var entityBaseType = typeof(TEntityBase);
            var entityParameter = Expression.Parameter(entityBaseType, "entity");

            var idProperty = entityBaseType.GetProperty("Id",
                                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                        binder: default,
                                                        typeof(TId),
                                                        new Type[0],
                                                        new ParameterModifier[0]);

            if (idProperty == null || !idProperty.CanRead)
            {
                throw new Exception(); // TODO
            }

            var concurrencyTokenProperty = entityBaseType.GetProperty("ConcurrencyToken",
                                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                        binder: default,
                                                        typeof(Guid),
                                                        new Type[0],
                                                        new ParameterModifier[0]);

            if (concurrencyTokenProperty == null || !concurrencyTokenProperty.CanRead || !concurrencyTokenProperty.CanWrite)
            {
                throw new Exception(); // TODO
            }

            var revisionProperty = entityBaseType.GetProperty("Revision",
                                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                        binder: default,
                                                        typeof(long),
                                                        new Type[0],
                                                        new ParameterModifier[0]);

            if (revisionProperty == null || !revisionProperty.CanRead || !revisionProperty.CanWrite)
            {
                throw new Exception(); // TODO
            }

            var uncommittedEventsProperty = entityBaseType.GetProperty("UncommittedEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (uncommittedEventsProperty == null ||
                !uncommittedEventsProperty.CanRead ||
                uncommittedEventsProperty.GetIndexParameters().Length != 0 ||
                !typeof(IEnumerable<TEventBase>).IsAssignableFrom(uncommittedEventsProperty.PropertyType))
            {
                throw new Exception(); // TODO
            }

            var commitEventsMethod = entityBaseType.GetMethod("CommitEvents",
                                                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                                binder: default,
                                                                new Type[0],
                                                                new ParameterModifier[0]);

            if (commitEventsMethod == null)
            {
                throw new Exception(); // TODO
            }

            var idPropertyAccess = Expression.Property(entityParameter, idProperty);
            _idAccessor = Expression.Lambda<Func<TEntityBase, TId>>(idPropertyAccess, entityParameter).Compile();

            var concurrencyTokenParameter = Expression.Parameter(typeof(Guid), "concurrencyToken");
            var concurrencyTokenPropertyAccess = Expression.Property(entityParameter, concurrencyTokenProperty);
            var concurrencyTokenPropertyAssign = Expression.Assign(concurrencyTokenPropertyAccess, concurrencyTokenParameter);
            _concurrencyTokenAccessor = Expression.Lambda<Func<TEntityBase, Guid>>(concurrencyTokenPropertyAccess, entityParameter).Compile();
            _concurrencyTokenSetter = Expression.Lambda<Action<TEntityBase, Guid>>(concurrencyTokenPropertyAssign, entityParameter, concurrencyTokenParameter).Compile();

            var revisionParameter = Expression.Parameter(typeof(long), "revision");
            var revisionPropertyAccess = Expression.Property(entityParameter, revisionProperty);
            var revisionPropertyAssign = Expression.Assign(revisionPropertyAccess, revisionParameter);
            _revisionAccessor = Expression.Lambda<Func<TEntityBase, long>>(revisionPropertyAccess, entityParameter).Compile();
            _revisionSetter = Expression.Lambda<Action<TEntityBase, long>>(revisionPropertyAssign, entityParameter, revisionParameter).Compile();

            var uncommittedEventsPropertyAccess = Expression.Property(entityParameter, uncommittedEventsProperty);
            _uncommittedEventsAccessor = Expression.Lambda<Func<TEntityBase, IEnumerable<TEventBase>>>(uncommittedEventsPropertyAccess, entityParameter).Compile();

            var commitMethodCall = Expression.Call(entityParameter, commitEventsMethod);
            _commitMethodInvoker = Expression.Lambda<Action<TEntityBase>>(commitMethodCall, entityParameter).Compile();
        }

        public TId GetId(TEntityBase entity)
        {
            return _idAccessor(entity);
        }

        public Guid GetConcurrencyToken(TEntityBase entity)
        {
            return _concurrencyTokenAccessor(entity);
        }

        public void SetConcurrencyToken(TEntityBase entity, Guid concurrencyToken)
        {
            _concurrencyTokenSetter(entity, concurrencyToken);
        }

        public long GetRevision(TEntityBase entity)
        {
            return _revisionAccessor(entity);
        }

        public void SetRevision(TEntityBase entity, long revision)
        {
            _revisionSetter(entity, revision);
        }

        public void CommitEvents(TEntityBase entity)
        {
            _commitMethodInvoker(entity);
        }

        public IEnumerable<TEventBase> GetUncommittedEvents(TEntityBase entity)
        {
            return _uncommittedEventsAccessor(entity);
        }
    }
}
