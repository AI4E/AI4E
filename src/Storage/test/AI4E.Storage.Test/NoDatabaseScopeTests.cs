using System.Linq;
using System.Threading.Tasks;
using AI4E.Storage.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed class NoDatabaseScopeTests
    {
        private NoDatabaseScope DatabaseScope { get; } = new NoDatabaseScope();

        [Fact]
        public async Task TryCommitOnNewScopeFailsTest()
        {
            var success = await DatabaseScope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task GetAsyncCanReadStoredEntriesTest()
        {
            var entry1 = new IdEntry() { Id = 1 };
            var entry2 = new IdEntry() { Id = 2 };
            await DatabaseScope.StoreAsync(entry1, cancellation: default);
            await DatabaseScope.StoreAsync(entry2, cancellation: default);

            var entries = await DatabaseScope.GetAsync<IdEntry>(p => p.Id == 1, cancellation: default).ToListAsync();

            Assert.Single(entries);
            Assert.Equal(entry1, entries.First());
        }

        [Fact]
        public async Task GetAsyncDoesNotReadRemovedEntriesTest()
        {
            var entry1 = new IdEntry() { Id = 1 };
            var entry2 = new IdEntry() { Id = 2 };
            var entry3 = new IdEntry() { Id = 2 };
            await DatabaseScope.StoreAsync(entry1, cancellation: default);
            await DatabaseScope.StoreAsync(entry2, cancellation: default);
            await DatabaseScope.StoreAsync(entry3, cancellation: default);
            await DatabaseScope.RemoveAsync(entry2, cancellation: default);
            var entries = await DatabaseScope.GetAsync<IdEntry>(p => p.Id < 3, cancellation: default).ToListAsync();

            Assert.Single(entries);
            Assert.Equal(entry1, entries.First());
        }

        [Fact]
        public async Task QueryAsyncCanReadStoredEntriesTest()
        {
            var entry1 = new IdEntry() { Id = 1 };
            var entry2 = new IdEntry() { Id = 2 };
            await DatabaseScope.StoreAsync(entry1, cancellation: default);
            await DatabaseScope.StoreAsync(entry2, cancellation: default);

            var entries = await DatabaseScope.QueryAsync<IdEntry, IdEntry>(query => query.Where(p => p.Id == 1), cancellation: default).ToListAsync();

            Assert.Single(entries);
            Assert.Equal(entry1, entries.First());
        }

        [Fact]
        public async Task QueryAsyncDoesNotReadRemovedEntriesTest()
        {
            var entry1 = new IdEntry() { Id = 1 };
            var entry2 = new IdEntry() { Id = 2 };
            var entry3 = new IdEntry() { Id = 2 };
            await DatabaseScope.StoreAsync(entry1, cancellation: default);
            await DatabaseScope.StoreAsync(entry2, cancellation: default);
            await DatabaseScope.StoreAsync(entry3, cancellation: default);
            await DatabaseScope.RemoveAsync(entry2, cancellation: default);
            var entries = await DatabaseScope.QueryAsync<IdEntry, IdEntry>(query => query.Where(p => p.Id < 3), cancellation: default).ToListAsync();

            Assert.Single(entries);
            Assert.Equal(entry1, entries.First());
        }

        [Fact]
        public async Task RollbackAsyncClearsEntriesTest()
        {
            var entry = new IdEntry();
            var entries = await DatabaseScope.GetAsync<IdEntry>(_ => true, cancellation: default).ToListAsync();

            await DatabaseScope.RollbackAsync(cancellation: default);

            Assert.Empty(entries);
        }

        [Fact]
        public async Task TryCommitAsyncClearsEntriesTest()
        {
            var entry = new IdEntry();
            var entries = await DatabaseScope.GetAsync<IdEntry>(_ => true, cancellation: default).ToListAsync();

            await DatabaseScope.TryCommitAsync(cancellation: default);

            Assert.Empty(entries);
        }

        [Fact]
        public async Task DisposeClearsEntriesTest()
        {
            var entry = new IdEntry();
            var entries = await DatabaseScope.GetAsync<IdEntry>(_ => true, cancellation: default).ToListAsync();

            DatabaseScope.Dispose();

            Assert.Empty(entries);
        }

        [Fact]
        public async Task DisposeAsyncClearsEntriesTest()
        {
            var entry = new IdEntry();
            var entries = await DatabaseScope.GetAsync<IdEntry>(_ => true, cancellation: default).ToListAsync();

            await DatabaseScope.DisposeAsync();

            Assert.Empty(entries);
        }
    }
}
