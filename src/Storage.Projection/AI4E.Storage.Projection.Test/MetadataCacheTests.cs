using System.Linq;
using System.Threading.Tasks;
using AI4E.Storage.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Projection
{
    [TestClass]
    public class MetadataCacheTests
    {
        private IDatabase Database { get; set; }
        private MetadataCache<string, TestMetadata> MetadataCache { get; set; }

        [TestInitialize]
        public async Task TestInitialize()
        {
            Database = new InMemoryDatabase();
            await Database.AddAsync(new TestMetadata { Id = "a" });
            await Database.AddAsync(new TestMetadata { Id = "b" });
            await Database.AddAsync(new TestMetadata { Id = "c" });

            MetadataCache = new MetadataCache<string, TestMetadata>(
                Database,
                metadata => metadata.Id,
                id => entry => entry.Id == id);
        }

        [TestMethod]
        public async Task GetNonExistingEntryTest()
        {
            var entry = await MetadataCache.GetEntryAsync("d", default);
            Assert.IsNull(entry);
            Assert.AreEqual(0, MetadataCache.GetTrackedEntries().Count());
        }

        [TestMethod]
        public async Task GetEntryTest()
        {
            var entry = await MetadataCache.GetEntryAsync("a", default);
            Assert.IsNotNull(entry);
            Assert.AreEqual("a", entry.Id);
            Assert.AreEqual(1, MetadataCache.GetTrackedEntries().Count());
            Assert.AreEqual("a", MetadataCache.GetTrackedEntries().First().Entry.Id);
            Assert.AreEqual("a", MetadataCache.GetTrackedEntries().First().OriginalEntry.Id);
            Assert.AreEqual(MetadataCacheEntryState.Unchanged, MetadataCache.GetTrackedEntries().First().State);
        }

        [TestMethod]
        public async Task UpdateEntryTest()
        {
            var entry = await MetadataCache.GetEntryAsync("a", default);
            entry.Value = "x";
            await MetadataCache.UpdateEntryAsync(entry, default);

            entry = await MetadataCache.GetEntryAsync("a", default);
            Assert.IsNotNull(entry);
            Assert.AreEqual("a", entry.Id);
            Assert.AreEqual("x", entry.Value);
            Assert.AreEqual(1, MetadataCache.GetTrackedEntries().Count());
            Assert.AreEqual("a", MetadataCache.GetTrackedEntries().First().Entry.Id);
            Assert.AreEqual("x", MetadataCache.GetTrackedEntries().First().Entry.Value);
            Assert.AreEqual("a", MetadataCache.GetTrackedEntries().First().OriginalEntry.Id);
            Assert.AreEqual(null, MetadataCache.GetTrackedEntries().First().OriginalEntry.Value);
            Assert.AreEqual(MetadataCacheEntryState.Updated, MetadataCache.GetTrackedEntries().First().State);
        }

        [TestMethod]
        public async Task UpdateEntryUntrackedTest()
        {
            var entry = new TestMetadata { Id = "a", Value = "x" };
            await MetadataCache.UpdateEntryAsync(entry, default);

            entry = await MetadataCache.GetEntryAsync("a", default);
            Assert.IsNotNull(entry);
            Assert.AreEqual("a", entry.Id);
            Assert.AreEqual("x", entry.Value);
            Assert.AreEqual(MetadataCacheEntryState.Updated, MetadataCache.GetTrackedEntries().First().State);
        }

        [TestMethod]
        public async Task CreateEntryTest()
        {
            var entry = new TestMetadata { Id = "d", Value = "x" };
            await MetadataCache.UpdateEntryAsync(entry, default);

            entry = await MetadataCache.GetEntryAsync("d", default);
            Assert.IsNotNull(entry);
            Assert.AreEqual("d", entry.Id);
            Assert.AreEqual("x", entry.Value);
            Assert.AreEqual(1, MetadataCache.GetTrackedEntries().Count());
            Assert.AreEqual("d", MetadataCache.GetTrackedEntries().First().Entry.Id);
            Assert.AreEqual("x", MetadataCache.GetTrackedEntries().First().Entry.Value);
            Assert.AreEqual(null, MetadataCache.GetTrackedEntries().First().OriginalEntry);
            Assert.AreEqual(MetadataCacheEntryState.Created, MetadataCache.GetTrackedEntries().First().State);
        }

        [TestMethod]
        public async Task DeleteEntryTest()
        {
            var entry = await MetadataCache.GetEntryAsync("a", default);
            await MetadataCache.DeleteEntryAsync(entry, default);

            entry = await MetadataCache.GetEntryAsync("a", default);
            Assert.IsNull(entry);
            Assert.AreEqual(1, MetadataCache.GetTrackedEntries().Count());
            Assert.AreEqual(null, MetadataCache.GetTrackedEntries().First().Entry);
            Assert.AreEqual("a", MetadataCache.GetTrackedEntries().First().OriginalEntry.Id);
            Assert.AreEqual(null, MetadataCache.GetTrackedEntries().First().OriginalEntry.Value);
            Assert.AreEqual(MetadataCacheEntryState.Deleted, MetadataCache.GetTrackedEntries().First().State);
        }

        [TestMethod]
        public async Task DeleteEntryNonExistingTest()
        {
            await MetadataCache.DeleteEntryAsync(new TestMetadata { Id = "d" }, default);

            var entry = await MetadataCache.GetEntryAsync("d", default);
            Assert.IsNull(entry);
            Assert.AreEqual(0, MetadataCache.GetTrackedEntries().Count());
        }

        [TestMethod]
        public async Task DeleteEntryUntrackedTest()
        {
            await MetadataCache.DeleteEntryAsync(new TestMetadata { Id = "a" }, default);

            var entry = await MetadataCache.GetEntryAsync("a", default);
            Assert.IsNull(entry);
            Assert.AreEqual(1, MetadataCache.GetTrackedEntries().Count());
            Assert.AreEqual(null, MetadataCache.GetTrackedEntries().First().Entry);
            Assert.AreEqual("a", MetadataCache.GetTrackedEntries().First().OriginalEntry.Id);
            Assert.AreEqual(null, MetadataCache.GetTrackedEntries().First().OriginalEntry.Value);
            Assert.AreEqual(MetadataCacheEntryState.Deleted, MetadataCache.GetTrackedEntries().First().State);
        }

        [TestMethod]
        public void GetEntryStateUntrackedTest()
        {
            var state = MetadataCache.GetEntryState(new TestMetadata { Id = "a" });

            Assert.AreEqual(MetadataCacheEntryState.Untracked, state);
        }

        [TestMethod]
        public async Task GetEntryStateUnchangedTest()
        {
            var entry = await MetadataCache.GetEntryAsync("a", default);
            var state = MetadataCache.GetEntryState(entry);

            Assert.AreEqual(MetadataCacheEntryState.Unchanged, state);
        }

        [TestMethod]
        public async Task GetEntryStateCreatedTest()
        {
            var entry = new TestMetadata { Id = "d", Value = "x" };
            await MetadataCache.UpdateEntryAsync(entry, default);
            var state = MetadataCache.GetEntryState(entry);

            Assert.AreEqual(MetadataCacheEntryState.Created, state);
        }

        [TestMethod]
        public async Task GetEntryStateUpdatedTest()
        {
            var entry = await MetadataCache.GetEntryAsync("a", default);
            entry.Value = "x";
            await MetadataCache.UpdateEntryAsync(entry, default);
            var state = MetadataCache.GetEntryState(entry);

            Assert.AreEqual(MetadataCacheEntryState.Updated, state);
        }

        [TestMethod]
        public async Task GetEntryStateDeletedTest()
        {
            var entry = await MetadataCache.GetEntryAsync("a", default);
            await MetadataCache.DeleteEntryAsync(entry, default);
            var state = MetadataCache.GetEntryState(entry);

            Assert.AreEqual(MetadataCacheEntryState.Deleted, state);
        }
    }

    public sealed class TestMetadata
    {
        public string Id { get; set; }
        public string Value { get; set; }
    }
}
