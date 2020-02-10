using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nito.AsyncEx;
using static AI4E.Storage.MongoDB.MongoExceptionHelper;

namespace AI4E.Storage.MongoDB
{
    public partial class MongoDatabase
	{
        private const string _collectionLookupEntryResourceKey = "collection-lookup-entry";
        private const string _counterEntryCollectionName = "data-store-counter";
        private const string _collectionLookupCollectionName = "data-store-collection-lookup";

        private volatile ImmutableDictionary<Type, Task> _collections = ImmutableDictionary<Type, Task>.Empty;
        private readonly object _collectionsLock = new object();

        private readonly IMongoCollection<CounterEntry> _counterCollection;
        private readonly IMongoCollection<CollectionLookupEntry> _collectionLookupCollection;

        private Task<IMongoCollection<TEntry>> GetCollectionAsync<TEntry>(CancellationToken cancellation)
        {
            return GetCollectionAsync<TEntry>(isInTransaction: false, cancellation)!;
        }

        internal Task<IMongoCollection<TEntry>?> GetCollectionAsync<TEntry>(bool isInTransaction, CancellationToken cancellation)
        {
            Task<IMongoCollection<TEntry>> result;

            if (_collections.TryGetValue(typeof(TEntry), out var entry))
            {
                result = (Task<IMongoCollection<TEntry>>)entry;
            }
            else
            {
                lock (_collectionsLock)
                {
                    if (_collections.TryGetValue(typeof(TEntry), out entry))
                    {
                        result = (Task<IMongoCollection<TEntry>>)entry;
                    }
                    else
                    {
                        var lazy = new AsyncLazy<IMongoCollection<TEntry>>(
                                            CreateCollectionAsync<TEntry>,
                                            AsyncLazyFlags.ExecuteOnCallingThread | AsyncLazyFlags.RetryOnFailure);

                        result = lazy.Task;
                        _collections = _collections.Add(typeof(TEntry), result);
                    }
                }
            }


            if (!isInTransaction || result.IsCompleted)
            {
                return result!;
            }

            // Cannot create a collection while beeing in a transaction.
            return Task.FromResult<IMongoCollection<TEntry>?>(null);
        }

        private async Task<IMongoCollection<TEntry>> CreateCollectionAsync<TEntry>()
        {
            var collectionKey = GetCollectionKey<TEntry>();
            var collectionName = await GetCollectionNameAsync(collectionKey).ConfigureAwait(false);
            var collection = await GetCollectionCoreAsync<TEntry>(collectionName).ConfigureAwait(false);
            return collection;
        }

        private static string GetCollectionKey<TEntry>()
        {
            return typeof(TEntry).ToString();
        }

        private async Task<string> GetCollectionNameAsync(string collectionKey)
        {
            var lookupEntry = await _collectionLookupCollection
                .AsQueryable()
                .FirstOrDefaultAsync(p => p.CollectionKey == collectionKey)
                .ConfigureAwait(false);

            if (lookupEntry != null)
            {
                return lookupEntry.CollectionName;
            }

            // There is no collection entry. We have to allocate one. Get a unique key.
            var collectionId = await GetUniqueResourceIdAsync(_collectionLookupEntryResourceKey, cancellation: default); // TODO: Use the disposal token

            try
            {
                lookupEntry = new CollectionLookupEntry { CollectionKey = collectionKey, CollectionName = "data-store#" + collectionId };
                await TryOperation(() => _collectionLookupCollection.InsertOneAsync(lookupEntry, default, cancellationToken: default)).ConfigureAwait(false); // TODO: Use the disposal token
            }
            catch (DuplicateKeyException)
            {
                lookupEntry = await _collectionLookupCollection
                    .AsQueryable()
                    .FirstOrDefaultAsync(p => p.CollectionKey == collectionKey)
                    .ConfigureAwait(false);
            }

            Debug.Assert(lookupEntry != null);

            return lookupEntry!.CollectionName;
        }

        private async Task<bool> CollectionExistsAsync(string collectionName)
        {
            var collections = await UnderlyingDatabase
                .ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", collectionName) })
                .ConfigureAwait(false);

            return await collections.AnyAsync().ConfigureAwait(false);
        }

        private async Task<IMongoCollection<TEntry>> GetCollectionCoreAsync<TEntry>(string collectionName)
        {
            while (!await CollectionExistsAsync(collectionName).ConfigureAwait(false))
            {
                try
                {
                    UnderlyingDatabase.CreateCollection(collectionName);
                }
                catch (MongoCommandException) { } // TODO: Check error code
            }

            var result = UnderlyingDatabase.GetCollection<TEntry>(collectionName);
            return result;
        }

#nullable disable
        private sealed class CollectionLookupEntry
        {
            [BsonId]
            public string CollectionKey { get; set; }

            public string CollectionName { get; set; }
        }
#nullable restore
    }
}
