using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    // TODO: Rename
    public sealed class EntityStoragePropertyManager : IEntityStoragePropertyManager
    {
        // TODO: Rename
        private readonly ConcurrentDictionary<Type, CacheEntry> _typedManagers = new ConcurrentDictionary<Type, CacheEntry>();

        public string GetConcurrencyToken(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return GetTypedManager(entity.GetType()).GetConcurrencyToken(entity);
        }

        public void SetConcurrencyToken(object entity, string concurrencyToken)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (concurrencyToken == null)
                throw new ArgumentNullException(nameof(concurrencyToken));

            GetTypedManager(entity.GetType()).SetConcurrencyToken(entity, concurrencyToken);
        }

        public long GetRevision(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return GetTypedManager(entity.GetType()).GetRevision(entity);
        }

        public void SetRevision(object entity, long revision)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            GetTypedManager(entity.GetType()).SetRevision(entity, revision);
        }

        public void CommitEvents(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            GetTypedManager(entity.GetType()).CommitEvents(entity);
        }

        public IEnumerable<object> GetUncommittedEvents(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return GetTypedManager(entity.GetType()).GetUncommittedEvents(entity);
        }

        // TODO: Rename
        private CacheEntry GetTypedManager(Type entityType)
        {
            return _typedManagers.GetOrAdd(entityType, BuildTypedManager);
        }

        // TODO: Rename
        private CacheEntry BuildTypedManager(Type entityType)
        {
            return new CacheEntry(entityType);
        }

        // TODO: Rename
        private readonly struct CacheEntry
        {
            private readonly MetadataStorage _metadataStorage;

            private readonly Func<object, string> _concurrencyTokenGetAccessor;
            private readonly Action<object, string> _concurrencyTokenSetAccessor;
            private readonly Func<object, long> _revisionGetAccessor;
            private readonly Action<object, long> _revisionSetAccessor;
            private readonly Func<object, IEnumerable<object>> _uncommittedEventsGetAccessor;
            private readonly Action<object> _commitEventsInvoker;

            public CacheEntry(Type entityType)
            {
                Assert(entityType != null);

                var metadataStorage = default(MetadataStorage);

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
                                 out _commitEventsInvoker);

                _metadataStorage = metadataStorage;
            }

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

            private static void BuildEventAccess(Type entityType,
                                                 ref MetadataStorage metadataStorage,
                                                 out Func<object, IEnumerable<object>> _uncommittedEventsGetAccessor,
                                                 out Action<object> commitEventsInvoker)
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

                    _uncommittedEventsGetAccessor = GetUncommittedEventsFromMetadata;
                    commitEventsInvoker = CommitEventsInMetadata;
                    return;
                }

                var entityParameter = Expression.Parameter(typeof(object), "entity");
                var convertedEntity = Expression.Convert(entityParameter, entityType);
                var uncommittedEventsPropertyAccess = Expression.Property(convertedEntity, uncommittedEventsProperty);
                _uncommittedEventsGetAccessor = Expression.Lambda<Func<object, IEnumerable<object>>>(
                    uncommittedEventsPropertyAccess,
                    entityParameter).Compile();

                var commitEventsMethodCall = Expression.Call(convertedEntity, commitEventsMethod);
                commitEventsInvoker = Expression.Lambda<Action<object>>(commitEventsMethodCall, entityParameter).Compile();
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

                public EntityMetadata() { }

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
            }
        }

        private interface IEntityMetadata
        {
            void CommitEvents();
            string GetConcurrencyToken();
            long GetRevision();
            IEnumerable<object> GetUncommittedEvents();
            void SetConcurrencyToken(string concurrencyToken);
            void SetRevision(long revision);
        }
    }
}
