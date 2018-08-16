using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    public sealed class EntityPropertyAccessor : IEntityPropertyAccessor
    {
        private readonly ConcurrentDictionary<Type, TypedEntityPropertyAccessor> _typedAccessors = new ConcurrentDictionary<Type, TypedEntityPropertyAccessor>();

        #region IEntityPropertyAccessor

        public bool TryGetId(Type entityType, object entity, out string id)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedPropertyAccessor(entityType);

            if (typedAccessor.CanGetId)
            {
                id = typedAccessor.GetId(entity);
                return true;
            }

            id = default;
            return false;
        }

        public bool TrySetId(Type entityType, object entity, string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedPropertyAccessor(entityType);

            if (typedAccessor.CanSetId)
            {
                typedAccessor.SetId(entity, id);
                return true;
            }

            return false;
        }

        public string GetConcurrencyToken(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            return GetTypedPropertyAccessor(entityType).GetConcurrencyToken(entity);
        }

        public void SetConcurrencyToken(Type entityType, object entity, string concurrencyToken)
        {
            if (concurrencyToken == null)
                throw new ArgumentNullException(nameof(concurrencyToken));

            CheckArguments(entityType, entity);

            GetTypedPropertyAccessor(entityType).SetConcurrencyToken(entity, concurrencyToken);
        }

        public long GetRevision(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            return GetTypedPropertyAccessor(entityType).GetRevision(entity);
        }

        public void SetRevision(Type entityType, object entity, long revision)
        {
            CheckArguments(entityType, entity);

            GetTypedPropertyAccessor(entityType).SetRevision(entity, revision);
        }

        public void CommitEvents(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            GetTypedPropertyAccessor(entityType).CommitEvents(entity);
        }

        public IEnumerable<object> GetUncommittedEvents(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            return GetTypedPropertyAccessor(entityType).GetUncommittedEvents(entity);
        }

        public bool TryAddEvent(Type entityType, object entity, object evt)
        {
            CheckArguments(entityType, entity);

            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            var typedAccessor = GetTypedPropertyAccessor(entityType);

            if (typedAccessor.CanAddEvent)
            {
                typedAccessor.AddEvent(entity, evt);
                return true;
            }

            return false;
        }

        #endregion

        private static void CheckArguments(Type entityType, object entity)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (!entityType.IsClass || typeof(Delegate).IsAssignableFrom(entityType))
                throw new ArgumentException("The argument must be a reference type that is not a delegate.", nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entityType.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be an open type definition.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The specified entity must be of type '{entityType.FullName}' or an assignable type.");
        }

        private TypedEntityPropertyAccessor GetTypedPropertyAccessor(Type entityType)
        {
            return _typedAccessors.GetOrAdd(entityType, BuildTypedPropertyAccessor);
        }

        private TypedEntityPropertyAccessor BuildTypedPropertyAccessor(Type entityType)
        {
            return new TypedEntityPropertyAccessor(entityType);
        }

        private readonly struct TypedEntityPropertyAccessor
        {
            private readonly MetadataStorage _metadataStorage;

            private readonly Func<object, string> _idGetAccessor;
            private readonly Action<object, string> _idSetAccessor;
            private readonly Func<object, string> _concurrencyTokenGetAccessor;
            private readonly Action<object, string> _concurrencyTokenSetAccessor;
            private readonly Func<object, long> _revisionGetAccessor;
            private readonly Action<object, long> _revisionSetAccessor;
            private readonly Func<object, IEnumerable<object>> _uncommittedEventsGetAccessor;
            private readonly Action<object> _commitEventsInvoker;
            private readonly Action<object, object> _addEventAccessor;

            public TypedEntityPropertyAccessor(Type entityType)
            {
                Assert(entityType != null);

                var metadataStorage = default(MetadataStorage);

                BuildIdAccess(entityType,
                              ref metadataStorage,
                              out _idGetAccessor,
                              out _idSetAccessor);

                BuildConcurrencyTokenAccess(entityType,
                                            ref metadataStorage,
                                            out _concurrencyTokenGetAccessor,
                                            out _concurrencyTokenSetAccessor);
                BuildRevisionAccess(entityType,
                                    ref metadataStorage,
                                    out _revisionGetAccessor,
                                    out _revisionSetAccessor);

                BuildEventAccess(entityType,
                                 ref metadataStorage,
                                 out _uncommittedEventsGetAccessor,
                                 out _commitEventsInvoker,
                                 out _addEventAccessor);

                _metadataStorage = metadataStorage;
            }

            #region Id

            public bool CanGetId => _idGetAccessor != null;
            public bool CanSetId => _idSetAccessor != null;

            public string GetId(object entity)
            {
                Assert(entity != null);

                if (!CanGetId)
                {
                    throw new NotSupportedException();
                }

                return _idGetAccessor(entity);
            }

            public void SetId(object entity, string id)
            {
                Assert(entity != null);
                Assert(id != null);

                if (!CanSetId)
                {
                    throw new NotSupportedException();
                }

                _idSetAccessor(entity, id);
            }

            private static MemberInfo GetIdMember(Type entityType)
            {
                return DataPropertyHelper.GetIdMember(entityType);
            }

            private static void BuildIdAccess(Type entityType,
                                              ref MetadataStorage metadataStorage,
                                              out Func<object, string> idGetAccessor,
                                               out Action<object, string> idSetAccessor)
            {
                var idMember = GetIdMember(entityType);

                if (idMember == null)
                {
                    var ms = metadataStorage = metadataStorage ?? new MetadataStorage();

                    string GetIdFromMetadata(object entity)
                    {
                        return ms.GetMetadata(entity).GetId();
                    }

                    void SetIdToMetadata(object entity, string id)
                    {
                        ms.GetMetadata(entity).SetId(id);
                    }

                    idGetAccessor = GetIdFromMetadata;
                    idSetAccessor = SetIdToMetadata;
                    return;
                }

                // We could theoretically allow setting id to an entity, 
                // but this will need support for transforming a stringified id back to its original representation.
                idSetAccessor = null;

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
                    idGetAccessor = Expression.Lambda<Func<object, string>>(toStringCall, entityParameter).Compile();
                }
                else
                {
                    idGetAccessor = null;
                }
            }

            #endregion

            #region ConcurrencyToken

            public string GetConcurrencyToken(object entity)
            {
                Assert(entity != null);
                return _concurrencyTokenGetAccessor(entity);
            }

            public void SetConcurrencyToken(object entity, string concurrencyToken)
            {
                Assert(entity != null);
                Assert(concurrencyToken != null);
                _concurrencyTokenSetAccessor(entity, concurrencyToken);
            }

            private static void BuildConcurrencyTokenAccess(Type entityType,
                                                            ref MetadataStorage metadataStorage,
                                                            out Func<object, string> concurrencyTokenGetAccessor,
                                                            out Action<object, string> concurrencyTokenSetAccessor)
            {
                var concurrencyTokenProperty = GetConcurrencyTokenProperty(entityType);

                if (concurrencyTokenProperty == null)
                {
                    var ms = metadataStorage = metadataStorage ?? new MetadataStorage();

                    string GetConcurrencyTokenFromMetadata(object entity)
                    {
                        return ms.GetMetadata(entity).GetConcurrencyToken();
                    }

                    void SetConcurrencyTokenToMetadata(object entity, string concurrencyToken)
                    {
                        ms.GetMetadata(entity).SetConcurrencyToken(concurrencyToken);
                    }

                    concurrencyTokenGetAccessor = GetConcurrencyTokenFromMetadata;
                    concurrencyTokenSetAccessor = SetConcurrencyTokenToMetadata;

                    return;
                }

                var entityParameter = Expression.Parameter(typeof(object), "entity");
                var convertedEntity = Expression.Convert(entityParameter, entityType);
                var concurrencyTokenPropertyAccess = Expression.Property(convertedEntity, concurrencyTokenProperty);

                Expression concurrencyTokenAccess = concurrencyTokenPropertyAccess;

                if (concurrencyTokenProperty.PropertyType != typeof(string))
                {
                    concurrencyTokenAccess = Expression.Convert(concurrencyTokenAccess, typeof(string));
                }

                concurrencyTokenGetAccessor = Expression.Lambda<Func<object, string>>(concurrencyTokenAccess, entityParameter)
                                                        .Compile();

                var concurrencyTokenParameter = Expression.Parameter(typeof(string), "concurrencyToken");
                var concurrencyTokenPropertyAssign = Expression.Assign(concurrencyTokenPropertyAccess, concurrencyTokenParameter);

                concurrencyTokenSetAccessor = Expression.Lambda<Action<object, string>>(concurrencyTokenPropertyAssign,
                                                                                        entityParameter,
                                                                                        concurrencyTokenParameter)
                                                        .Compile();
            }

            private static PropertyInfo GetConcurrencyTokenProperty(Type entityType)
            {
                var result = entityType.GetProperty("ConcurrencyToken",
                                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (result == null)
                    return null;

                if (result.GetIndexParameters().Length != 0)
                {
                    return null;
                }

                if (result.PropertyType != typeof(string) &&
                    result.PropertyType != typeof(object))
                {
                    return null;
                }

                if (!result.CanRead)
                {
                    return null;
                }

                if (!result.CanWrite)
                {
                    return null;
                }

                return result;
            }

            #endregion

            #region Revision

            public long GetRevision(object entity)
            {
                Assert(entity != null);
                return _revisionGetAccessor(entity);
            }

            public void SetRevision(object entity, long revision)
            {
                Assert(entity != null);
                _revisionSetAccessor(entity, revision);
            }

            private static void BuildRevisionAccess(Type entityType,
                                                    ref MetadataStorage metadataStorage,
                                                    out Func<object, long> revisionGetAccessor,
                                                    out Action<object, long> revisionSetAccessor)
            {
                var revisionProperty = GetRevisionProperty(entityType);

                if (revisionProperty == null)
                {
                    var ms = metadataStorage = metadataStorage ?? new MetadataStorage();

                    long GetRevisionFromMetadata(object entity)
                    {
                        return ms.GetMetadata(entity).GetRevision();
                    }

                    void SetRevisionToMetadata(object entity, long revision)
                    {
                        ms.GetMetadata(entity).SetRevision(revision);
                    }

                    revisionGetAccessor = GetRevisionFromMetadata;
                    revisionSetAccessor = SetRevisionToMetadata;

                    return;
                }

                var entityParameter = Expression.Parameter(typeof(object), "entity");
                var convertedEntity = Expression.Convert(entityParameter, entityType);
                var revisionPropertyAccess = Expression.Property(convertedEntity, revisionProperty);

                revisionGetAccessor = Expression.Lambda<Func<object, long>>(revisionPropertyAccess, entityParameter)
                                                .Compile();

                var revisionParameter = Expression.Parameter(typeof(long), "revision");
                var revisionPropertyAssign = Expression.Assign(revisionPropertyAccess, revisionParameter);

                revisionSetAccessor = Expression.Lambda<Action<object, long>>(revisionPropertyAssign,
                                                                              entityParameter,
                                                                              revisionParameter)
                                                .Compile();
            }

            private static PropertyInfo GetRevisionProperty(Type entityType)
            {
                var result = entityType.GetProperty("Revision",
                                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (result == null)
                    return null;

                if (result.GetIndexParameters().Length != 0)
                {
                    return null;
                }

                if (result.PropertyType != typeof(long))
                {
                    return null;
                }

                if (!result.CanRead)
                {
                    return null;
                }

                if (!result.CanWrite)
                {
                    return null;
                }

                return result;
            }

            #endregion

            #region Events

            public bool CanAddEvent => _addEventAccessor != null;

            public void CommitEvents(object entity)
            {
                Assert(entity != null);
                _commitEventsInvoker(entity);
            }

            public IEnumerable<object> GetUncommittedEvents(object entity)
            {
                Assert(entity != null);
                return _uncommittedEventsGetAccessor(entity);
            }

            public void AddEvent(object entity, object evt)
            {
                Assert(entity != null);
                Assert(evt != null);

                if (_addEventAccessor == null)
                {
                    throw new NotSupportedException();
                }

                _addEventAccessor(entity, evt);
            }

            private static void BuildEventAccess(Type entityType,
                                                 ref MetadataStorage metadataStorage,
                                                 out Func<object, IEnumerable<object>> uncommittedEventsGetAccessor,
                                                 out Action<object> commitEventsInvoker,
                                                 out Action<object, object> addEventAccessor)
            {
                var uncommittedEventsProperty = GetUncommittedEventsProperty(entityType);
                var commitEventsMethod = GetCommitEventsMethod(entityType);

                if (uncommittedEventsProperty == null ||
                    commitEventsMethod == null)
                {
                    var ms = metadataStorage = metadataStorage ?? new MetadataStorage();

                    IEnumerable<object> GetUncommittedEventsFromMetadata(object entity)
                    {
                        return ms.GetMetadata(entity).GetUncommittedEvents();
                    }

                    void CommitEventsInMetadata(object entity)
                    {
                        ms.GetMetadata(entity).CommitEvents();
                    }

                    void AddEventToMetadata(object entity, object evt)
                    {
                        ms.GetMetadata(entity).AddEvent(evt);
                    }

                    uncommittedEventsGetAccessor = GetUncommittedEventsFromMetadata;
                    commitEventsInvoker = CommitEventsInMetadata;
                    addEventAccessor = AddEventToMetadata;
                    return;
                }

                var entityParameter = Expression.Parameter(typeof(object), "entity");
                var convertedEntity = Expression.Convert(entityParameter, entityType);
                var uncommittedEventsPropertyAccess = Expression.Property(convertedEntity, uncommittedEventsProperty);
                uncommittedEventsGetAccessor = Expression.Lambda<Func<object, IEnumerable<object>>>(
                    uncommittedEventsPropertyAccess,
                    entityParameter).Compile();

                var commitEventsMethodCall = Expression.Call(convertedEntity, commitEventsMethod);
                commitEventsInvoker = Expression.Lambda<Action<object>>(commitEventsMethodCall, entityParameter).Compile();

                addEventAccessor = null;
            }

            private static PropertyInfo GetUncommittedEventsProperty(Type entityType)
            {
                var result = entityType.GetProperty("UncommittedEvents",
                                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (result == null)
                    return null;

                if (result.GetIndexParameters().Length != 0)
                {
                    return null;
                }

                if (!typeof(IEnumerable<object>).IsAssignableFrom(result.PropertyType))
                {
                    return null;
                }

                if (!result.CanRead)
                {
                    return null;
                }


                return result;
            }

            private static MethodInfo GetCommitEventsMethod(Type entityType)
            {
                // TODO
                var result = entityType.GetMethod("CommitEvents",
                                                  BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                  binder: default,
                                                  new Type[0],
                                                  new ParameterModifier[0]);

                return result;
            }

            #endregion
        }

        private sealed class MetadataStorage
        {
            private readonly ConditionalWeakTable<object, EntityMetadata> _storage;

            public MetadataStorage()
            {
                _storage = new ConditionalWeakTable<object, EntityMetadata>();
            }

            public IEntityMetadata GetMetadata(object entity)
            {
                Assert(entity != null);

                //return _metaDataStorage.GetOrCreateValue(entity);

                if (!_storage.TryGetValue(entity, out var result))
                {
                    result = new EntityMetadata();

                    _storage.Add(entity, result);
                }

                return result;
            }

            private sealed class EntityMetadata : IEntityMetadata
            {
                private readonly List<object> _uncommittedEvents = new List<object>();
                private long _revision = 0;
                private string _concurrencyToken = string.Empty;
                private string _id = string.Empty;

                public EntityMetadata() { }

                public string GetId()
                {
                    return _id;
                }

                public void SetId(string id)
                {
                    _id = id;
                }

                public string GetConcurrencyToken()
                {
                    return _concurrencyToken;
                }

                public void SetConcurrencyToken(string concurrencyToken)
                {
                    _concurrencyToken = concurrencyToken;
                }

                public long GetRevision()
                {
                    return _revision;
                }

                public void SetRevision(long revision)
                {
                    _revision = revision;
                }

                public void CommitEvents()
                {
                    _uncommittedEvents.Clear();
                }

                public IEnumerable<object> GetUncommittedEvents()
                {
                    return _uncommittedEvents.AsReadOnly();
                }

                public void AddEvent(object evt)
                {
                    _uncommittedEvents.Add(evt);
                }
            }
        }

        private interface IEntityMetadata
        {
            string GetId();
            void SetId(string id);
            void CommitEvents();
            string GetConcurrencyToken();
            long GetRevision();
            IEnumerable<object> GetUncommittedEvents();
            void SetConcurrencyToken(string concurrencyToken);
            void SetRevision(long revision);
            void AddEvent(object evt);
        }
    }
}
