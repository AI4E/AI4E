using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoCoordinationStorage : ICoordinationStorage, ISessionStorage
    {
        private readonly IMongoDatabase _database;
        private readonly IStoredSessionManager _storedSessionManager;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly IMongoCollection<MongoStoredEntry> _entryCollection;
        private readonly IMongoCollection<MongoStoredSession> _sessionCollection;

        public MongoCoordinationStorage(IMongoDatabase database, IStoredSessionManager storedSessionManager, IStoredEntryManager storedEntryManager)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (storedSessionManager == null)
                throw new ArgumentNullException(nameof(storedSessionManager));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            _database = database;
            _storedSessionManager = storedSessionManager;
            _storedEntryManager = storedEntryManager;

            _entryCollection = _database.GetCollection<MongoStoredEntry>("coordination/entries");
            _sessionCollection = _database.GetCollection<MongoStoredSession>("coordination/sessions");

            Assert(_entryCollection != null);
            Assert(_sessionCollection != null);
        }

        #region Entry

        public IStoredEntry CreateEntry(string path, string session, bool isEphemeral, byte[] value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return _storedEntryManager.Create(path, session, isEphemeral, value.ToImmutableArray());
        }

        public async Task<IStoredEntry> GetEntryAsync(string path, CancellationToken cancellation)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var queryable = _entryCollection.AsQueryable();

            Assert(queryable != null);

            var mongoStoredEntry = await queryable.FirstOrDefaultAsync(p => p.Path == path, cancellation);

            return _storedEntryManager.Copy(mongoStoredEntry);
        }

        public async Task<IStoredEntry> UpdateEntryAsync(IStoredEntry comparand, IStoredEntry value, CancellationToken cancellation)
        {
            string path;

            if (comparand != null && value != null)
            {
                if (comparand.Path != value.Path)
                {
                    throw new ArgumentException("The path of the comparand must be equal to the path of the new value.");
                }

                if (value.StorageVersion == comparand.StorageVersion)
                {
                    return value;
                }

                path = comparand.Path;
            }
            else if (comparand != null)
            {
                path = comparand.Path;
            }
            else if (value != null)
            {
                path = value.Path;
            }
            else // (value == null && comparand == null)
            {
                throw new ArgumentException("Either comparand or value may be null but not both.");
            }

            var convertedValue = ConvertValue(value);
            var comparandVersion = comparand?.StorageVersion ?? 0;

            if (value == null)
            {
                Assert(comparandVersion != 0);

                var deleteResult = await MongoWriteHelper.TryWriteOperation(() => _entryCollection.DeleteOneAsync(p => p.Path == path && p.StorageVersion == comparandVersion, cancellation));

                if (deleteResult.IsAcknowledged && deleteResult.DeletedCount != 0)
                {
                    return comparand;
                }
            }
            else if (comparandVersion != 0)
            {
                var replaceResult = await MongoWriteHelper.TryWriteOperation(() => _entryCollection.ReplaceOneAsync(p => p.Path == path && p.StorageVersion == comparandVersion, convertedValue, new UpdateOptions { IsUpsert = false }, cancellation));

                if (replaceResult.IsAcknowledged && replaceResult.MatchedCount != 0)
                {
                    return comparand;
                }
            }
            else
            {
                var entry = await GetEntryAsync(path, cancellation);

                if (entry != null)
                {
                    return entry;
                }

                try
                {
                    await MongoWriteHelper.TryWriteOperation(() => _entryCollection.InsertOneAsync(convertedValue, new InsertOneOptions { }, cancellation));

                    return null;
                }
                catch (ConcurrencyException) { }
            }

            // If we reach this, the operation was not successful
            return await GetEntryAsync(path, cancellation);
        }

        private MongoStoredEntry ConvertValue(IStoredEntry value)
        {
            MongoStoredEntry convertedValue = null;

            if (value != null)
            {
                convertedValue = value as MongoStoredEntry ?? new MongoStoredEntry(value);

                Assert(convertedValue != null);
                Assert(convertedValue.Path == value.Path);
            }

            return convertedValue;
        }

        #endregion

        #region Session

        public IStoredSession CreateSession(string key, DateTime leaseEnd)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storedSessionManager.Begin(key, leaseEnd);
        }

        public async Task<IStoredSession> GetSessionAsync(string key, CancellationToken cancellation)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var queryable = _sessionCollection.AsQueryable();

            Assert(queryable != null);

            var mongoStoredSession = await queryable.FirstOrDefaultAsync(p => p.Key == key, cancellation);

            if (mongoStoredSession == null)
                return null;

            return _storedSessionManager.Copy(mongoStoredSession);
        }

        public async Task<IEnumerable<IStoredSession>> GetSessionsAsync(CancellationToken cancellation)
        {
            var queryable = _sessionCollection.AsQueryable();

            Assert(queryable != null);

            return (await queryable.ToListAsync(cancellation)).Select(p => _storedSessionManager.Copy(p));
        }

        public async Task<IStoredSession> UpdateSessionAsync(IStoredSession comparand, IStoredSession value, CancellationToken cancellation)
        {
            string key;

            if (comparand != null && value != null)
            {
                if (comparand.Key != value.Key)
                {
                    throw new ArgumentException("The key of the comparand must be equal to the key of the new value.");
                }

                if (value.StorageVersion == comparand.StorageVersion)
                {
                    return value;
                }

                key = comparand.Key;
            }
            else if (comparand != null)
            {
                key = comparand.Key;
            }
            else if (value != null)
            {
                key = value.Key;
            }
            else // (value == null && comparand == null)
            {
                throw new ArgumentException("Either comparand or value may be null but not both.");
            }

            var convertedValue = ConvertValue(value);
            var comparandVersion = comparand?.StorageVersion ?? 0;

            if (value == null)
            {
                Assert(comparandVersion != 0);

                var deleteResult = await MongoWriteHelper.TryWriteOperation(() => _sessionCollection.DeleteOneAsync(p => p.Key == key && p.StorageVersion == comparandVersion, cancellation));

                if (deleteResult.IsAcknowledged && deleteResult.DeletedCount != 0)
                {
                    return comparand;
                }
            }
            else if (comparandVersion != 0)
            {
                var replaceResult = await MongoWriteHelper.TryWriteOperation(() => _sessionCollection.ReplaceOneAsync(p => p.Key == key && p.StorageVersion == comparandVersion, convertedValue, new UpdateOptions { IsUpsert = false }, cancellation));

                if (replaceResult.IsAcknowledged && replaceResult.MatchedCount != 0)
                {
                    return comparand;
                }
            }
            else
            {
                try
                {
                    await MongoWriteHelper.TryWriteOperation(() => _sessionCollection.InsertOneAsync(convertedValue, new InsertOneOptions { }, cancellation));

                    return null;
                }
                catch (ConcurrencyException) { }
            }

            // If we reach this, the operation was not successful
            return await GetSessionAsync(key, cancellation);
        }

        private MongoStoredSession ConvertValue(IStoredSession value)
        {
            MongoStoredSession convertedValue = null;

            if (value != null)
            {
                convertedValue = value as MongoStoredSession ?? new MongoStoredSession(value);

                Assert(convertedValue != null);
                Assert(convertedValue.Key == value.Key);
            }

            return convertedValue;
        }

        #endregion
    }

    internal sealed class MongoStoredEntry : IStoredEntry
    {
        [BsonConstructor]
        public MongoStoredEntry() { }

        public MongoStoredEntry(IStoredEntry entry)
        {
            Path = entry.Path;
            Value = entry.Value.ToArray();
            ReadLocks = entry.ReadLocks.ToArray();
            WriteLock = entry.WriteLock;
            Childs = entry.Childs.ToArray();
            CreationTime = entry.CreationTime;
            LastWriteTime = entry.LastWriteTime;
            Version = entry.Version;
            StorageVersion = entry.StorageVersion;
            EphemeralOwner = entry.EphemeralOwner;
        }

        [BsonId]
        public string Path { get; set; }
        public byte[] Value { get; set; }
        public string[] ReadLocks { get; set; }
        public string WriteLock { get; set; }
        public int Version { get; set; }
        public int StorageVersion { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string[] Childs { get; set; }
        public string EphemeralOwner { get; set; }

        ImmutableArray<byte> IStoredEntry.Value => Value.ToImmutableArray();
        ImmutableArray<string> IStoredEntry.ReadLocks => ReadLocks.ToImmutableArray();
        ImmutableArray<string> IStoredEntry.Childs => Childs.ToImmutableArray();

    }

    internal sealed class MongoStoredSession : IStoredSession
    {
        [BsonConstructor]
        public MongoStoredSession() { }

        public MongoStoredSession(IStoredSession session)
        {
            Key = session.Key;
            IsEnded = session.IsEnded;
            LeaseEnd = session.LeaseEnd;
            Entries = session.Entries.ToArray();
            StorageVersion = session.StorageVersion;
        }

        [BsonId]
        public string Key { get; set; }
        public bool IsEnded { get; set; }
        public DateTime LeaseEnd { get; set; }
        public string[] Entries { get; set; }
        public int StorageVersion { get; set; }

        ImmutableArray<string> IStoredSession.Entries => Entries.ToImmutableArray();
    }
}
