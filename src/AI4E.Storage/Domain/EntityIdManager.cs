using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    public sealed class EntityIdManager : IEntityIdManager
    {
        private readonly ConcurrentDictionary<Type, TypedEntityIdManager> _typedManagers;

        public EntityIdManager()
        {
            _typedManagers = new ConcurrentDictionary<Type, TypedEntityIdManager>();
        }

        public bool TryGetId(Type entityType, object entity, out string id)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entityType.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be an open type definition.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The specified entity must be of type '{entityType.FullName}' or an assignable type.");

            var typedManager = GetTypedManager(entityType);

            if (typedManager.CanGetId)
            {
                id = typedManager.GetId(entity);
                return true;
            }

            id = default;
            return false;
        }

        public bool TrySetId(Type entityType, object entity, string id)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (entityType.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be an open type definition.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The specified entity must be of type '{entityType.FullName}' or an assignable type.");

            var typedManager = GetTypedManager(entityType);

            if (typedManager.CanSetId)
            {
                typedManager.SetId(entity, id);
                return true;
            }

            return false;
        }

        private TypedEntityIdManager GetTypedManager(Type entityType)
        {
            return _typedManagers.GetOrAdd(entityType, BuildTypedManager);
        }

        private static TypedEntityIdManager BuildTypedManager(Type entityType)
        {
            return new TypedEntityIdManager(entityType);
        }

        private readonly struct TypedEntityIdManager
        {
            private readonly Type _entityType;
            private readonly Func<object, string> _idGetAccessor;
            private readonly Action<object, string> _idSetAccessor;

            public TypedEntityIdManager(Type entityType)
            {
                Assert(entityType != null);

                _entityType = entityType;

                var idMember = DataPropertyHelper.GetIdMember(entityType);

                if (idMember == null)
                {
                    var idTable = new ConditionalWeakTable<object, IdTableEntry>();

                    _idGetAccessor = obj => idTable.GetOrCreateValue(obj).Id;
                    _idSetAccessor = (obj, id) => idTable.GetOrCreateValue(obj).Id = id;
                }
                else
                {
                    // We could theoretically allow setting id to an entity, 
                    // but this will need support for transforming a stringified id back to its original representation.
                    _idSetAccessor = null;

                    var idType = DataPropertyHelper.GetIdType(entityType);
                    var entityParameter = Expression.Parameter(typeof(object), "entity");
                    var convertedEntity = Expression.Convert(entityParameter, entityType);

                    var idAccess = default(Expression);

                    if (idMember.MemberType == MemberTypes.Method)
                    {
                        idAccess = Expression.Call(convertedEntity, (MethodInfo)idMember);
                    }
                    else if (idMember.MemberType == MemberTypes.Field || idMember.MemberType == MemberTypes.Property)
                    {
                        idAccess = Expression.MakeMemberAccess(convertedEntity, idMember);
                    }

                    var toStringMethod = idType.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);

                    Assert(toStringMethod != null);

                    if (idAccess != null)
                    {
                        var toStringCall = Expression.Call(idAccess, toStringMethod);
                        _idGetAccessor = Expression.Lambda<Func<object, string>>(toStringCall, entityParameter).Compile();
                    }
                    else
                    {
                        _idGetAccessor = null;
                    }
                }
            }

            public bool CanGetId => _idGetAccessor != null;
            public bool CanSetId => _idSetAccessor != null;

            public string GetId(object entity)
            {
                Assert(_entityType != null);
                Assert(entity != null);

                if (!CanGetId)
                {
                    throw new NotSupportedException();
                }

                return _idGetAccessor(entity);
            }

            public void SetId(object entity, string id)
            {
                Assert(_entityType != null);
                Assert(entity != null);
                Assert(id != null);

                if (!CanSetId)
                {
                    throw new NotSupportedException();
                }

                _idSetAccessor(entity, id);
            }

            private sealed class IdTableEntry
            {
#pragma warning disable IDE0032
                private volatile string _id;
#pragma warning restore IDE0032

                public IdTableEntry()
                {
                    _id = null;
                }

                public string Id
                {
                    get => _id; // Volatile read op.
                    set => _id = value; // Volatile write op.
                }
            }
        }
    }
}
