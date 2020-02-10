using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB
{
    public partial class MongoDatabase
    {
        public async ValueTask<long> GetUniqueResourceIdAsync(string resourceKey, CancellationToken cancellation)
        {
            if (resourceKey == null)
                throw new ArgumentNullException(nameof(resourceKey));

            var counterEntry = await _counterCollection.FindOneAndUpdateAsync<CounterEntry, CounterEntry>(
                                p => p.ResourceKey == resourceKey,
                                Builders<CounterEntry>.Update.Inc(p => p.Counter, 1),
                                new FindOneAndUpdateOptions<CounterEntry, CounterEntry>
                                {
                                    ReturnDocument = ReturnDocument.After,
                                    IsUpsert = true
                                }, cancellation).ConfigureAwait(false);

            return counterEntry.Counter;
        }

#nullable disable
        private sealed class CounterEntry
        {
            [BsonId]
            public string ResourceKey { get; set; }

            public int Counter { get; set; }
        }
#nullable restore
    }
}
