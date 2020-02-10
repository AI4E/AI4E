using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Specification.Helpers;
using AI4E.Storage.Specification.TestTypes;
using Xunit;

#pragma warning disable CA2007

namespace AI4E.Storage.Specification
{
    public abstract class DatabaseSpecification
    {
        private readonly Lazy<IDatabase> _database;

        public DatabaseSpecification()
        {
            _database = new Lazy<IDatabase>(BuildDatabase, LazyThreadSafetyMode.None);
        }

        protected abstract IDatabase BuildDatabase();

        public IDatabase Database => _database.Value;

        [Fact]
        public void CreateScopeTest()
        {
            var supportsScopes = Database.SupportsScopes;

            if (!supportsScopes)
            {
                Assert.Throws<NotSupportedException>(() =>
                {
                    Database.CreateScope();
                });
            }
            else
            {
                var scope = Database.CreateScope();

                Assert.NotNull(scope);
            }
        }

        [Fact]
        public async Task AddAsyncThrowsOnNullEntryTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await Database.AddAsync<TestEntry>(null, cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncThrowsOnNullEntryTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await Database.UpdateAsync<TestEntry>(null, _ => true, cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncThrowsOnNullPredicateTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await Database.UpdateAsync(new TestEntry(), null, cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncFailsOnNonSatisfiedPredicateTest()
        {
            var success = await Database.UpdateAsync(new TestEntry() { Id = 1 }, _ => false, cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task RemoveAsyncThrowsOnNullEntryTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await Database.RemoveAsync<TestEntry>(null, _ => true, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncThrowsOnNullPredicateTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await Database.RemoveAsync(new TestEntry() { Id = 1 }, null, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncFailsOnNonSatisfiedPredicateTest()
        {
            var success = await Database.RemoveAsync(new TestEntry() { Id = 1 }, _ => false, cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task GetOneAsyncThrowsOnNullPredicateTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await Database.GetOneAsync<TestEntry>(null, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOneAsyncFailsOnNonSatisfiedPredicateTest()
        {
            var entry = await Database.GetOneAsync<TestEntry>(_ => false, cancellation: default);

            Assert.Null(entry);
        }

        [Fact]
        public async Task SingleEntryStorageTest()
        {
            var id = 5;
            var addSuccess = await Database.AddAsync(new MinimalEntry { Id = id }, cancellation: default);
            var entry = await Database.GetOneAsync<MinimalEntry>(p => p.Id == id, cancellation: default);

            Assert.True(addSuccess);
            Assert.NotNull(entry);
            Assert.Equal(id, entry.Id);
        }

        [Fact]
        public async Task GetAsyncTest()
        {
            await DatabaseSeed.SeedDatabaseAsync(Database);
            var entries = Database.GetAsync<MinimalEntry>(p => p.Id > 5, cancellation: default);

            Assert.NotNull(entries);
            Assert.Equal(5, await entries.CountAsync(cancellationToken: default));
            Assert.True(await entries.AllAsync(p => Enumerable.Range(6, 5).Contains(p.Id)));
        }

        [Fact]
        public async Task ClearTest()
        {
            await DatabaseSeed.SeedDatabaseAsync(Database);
            await Database.Clear<MinimalEntry>();

            var count = await Database.GetAsync<MinimalEntry>(_ => true, cancellation: default)
                                      .CountAsync(cancellationToken: default);
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ClearOtherEntryTypeTest()
        {
            await DatabaseSeed.SeedDatabaseAsync(Database);
            await Database.Clear<TestEntry>();

            var count = await Database.GetAsync<MinimalEntry>(_ => true, cancellation: default)
                                      .CountAsync(cancellationToken: default);
            Assert.Equal(10, count);
        }

        [Fact]
        public async Task UpdateAsyncTest()
        {
            await DatabaseSeed.SeedDatabaseAsync(Database);
            var success = await Database.UpdateAsync(new MinimalEntry { Id = 1, Value = 11 }, p => p.Value == 1, cancellation: default);

            var entries = Database.GetAsync<MinimalEntry>(p => p.Value == 1, cancellation: default);
            var oneIdEntry = await Database.GetOneAsync<MinimalEntry>(p => p.Value == 11, cancellation: default);

            Assert.True(success);
            Assert.NotNull(oneIdEntry);
            Assert.Equal(1, oneIdEntry.Id);
            Assert.NotNull(entries);
            Assert.Equal(9, await entries.CountAsync(cancellationToken: default));
            Assert.True(await entries.AllAsync(p => Enumerable.Range(2, 9).Contains(p.Id)));
        }

        [Fact]
        public async Task UpdateAsyncWithNonMatchingConditionTest()
        {
            await DatabaseSeed.SeedDatabaseAsync(Database);
            var success = await Database.UpdateAsync(new MinimalEntry { Id = 1, Value = 11 }, p => p.Value == 2, cancellation: default);

            var entries = Database.GetAsync<MinimalEntry>(p => p.Value == 1, cancellation: default);
            var oneIdEntry = await Database.GetOneAsync<MinimalEntry>(p => p.Value == 11, cancellation: default);

            Assert.False(success);
            Assert.Null(oneIdEntry);
            Assert.NotNull(entries);
            Assert.Equal(10, await entries.CountAsync(cancellationToken: default));
            Assert.True(await entries.AllAsync(p => Enumerable.Range(1, 10).Contains(p.Id)));
        }

        [Fact]
        public async Task RemoveAsyncTest()
        {
            await DatabaseSeed.SeedDatabaseAsync(Database);
            var success = await Database.RemoveAsync(new MinimalEntry { Id = 1 }, p => p.Value == 1, cancellation: default);

            var entries = Database.GetAsync<MinimalEntry>(p => p.Value == 1, cancellation: default);
            var oneIdEntry = await Database.GetOneAsync<MinimalEntry>(p => p.Id == 1, cancellation: default);

            Assert.True(success);
            Assert.Null(oneIdEntry);
            Assert.NotNull(entries);
            Assert.Equal(9, await entries.CountAsync(cancellationToken: default));
            Assert.True(await entries.AllAsync(p => Enumerable.Range(2, 9).Contains(p.Id)));
        }

        [Fact]
        public async Task RemoveAsyncWithNonMatchingConditionTest()
        {
            await DatabaseSeed.SeedDatabaseAsync(Database);
            var success = await Database.RemoveAsync(new MinimalEntry { Id = 1 }, p => p.Value == 2, cancellation: default);

            var entries = Database.GetAsync<MinimalEntry>(p => p.Value == 1, cancellation: default);
            var oneIdEntry = await Database.GetOneAsync<MinimalEntry>(p => p.Id == 1, cancellation: default);

            Assert.False(success);
            Assert.NotNull(oneIdEntry);
            Assert.Equal(1, oneIdEntry.Value);
            Assert.NotNull(entries);
            Assert.Equal(10, await entries.CountAsync(cancellationToken: default));
            Assert.True(await entries.AllAsync(p => Enumerable.Range(1, 10).Contains(p.Id)));
        }
    }
}
