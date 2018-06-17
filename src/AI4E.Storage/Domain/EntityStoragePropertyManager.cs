using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    // TODO: Rename
    // The service must be registered with the same scope than the storage-engine to ensure consistency.
    public sealed class EntityStoragePropertyManager : IEntityStoragePropertyManager
    {
        private readonly ConditionalWeakTable<object, EntityMetadata> _metaDataStorage;

        public EntityStoragePropertyManager()
        {
            _metaDataStorage = new ConditionalWeakTable<object, EntityMetadata>();
        }

        public string GetConcurrencyToken(object entity)
        {
            return GetMetadata(entity).GetConcurrencyToken();
        }

        public void SetConcurrencyToken(object entity, string concurrencyToken)
        {
            GetMetadata(entity).SetConcurrencyToken(concurrencyToken);
        }

        public long GetRevision(object entity)
        {
            return GetMetadata(entity).GetRevision();
        }

        public void SetRevision(object entity, long revision)
        {
            GetMetadata(entity).SetRevision(revision);
        }

        public void CommitEvents(object entity)
        {
            GetMetadata(entity).CommitEvents();
        }

        public IEnumerable<object> GetUncommittedEvents(object entity)
        {
            return GetMetadata(entity).GetUncommittedEvents();
        }

        private EntityMetadata GetMetadata(object entity)
        {
            Assert(entity != null);

            return _metaDataStorage.GetOrCreateValue(entity);

            //if (!_metaDataStorage.TryGetValue(entity, out var result))
            //{
            //    result = new EntityMetadata();

            //    _metaDataStorage.Add(entity, result);
            //}

            //return result;
        }

        private sealed class EntityMetadata
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
}
