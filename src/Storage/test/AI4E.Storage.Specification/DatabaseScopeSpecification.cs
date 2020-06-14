using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Storage.Specification.Helpers;
using AI4E.Storage.Specification.TestTypes;
using Nito.AsyncEx;
using Xunit;

#pragma warning disable CA2007

namespace AI4E.Storage.Specification
{
    public abstract class DatabaseScopeSpecification
    {
        private readonly AsyncLazy<IDatabase> _database;

        public DatabaseScopeSpecification()
        {
            _database = new AsyncLazy<IDatabase>(BuildDatabaseAsync);
        }

        protected abstract Task<IDatabase> BuildDatabaseAsync();

        public Task<IDatabase> GetDatabaseAsync() => _database.Task;

        [Fact]
        public async Task TryCommitAsyncSuccessWhenNoOperationPerformedTest()
        {
            var database = await GetDatabaseAsync();

            var success = await database.CreateScope().TryCommitAsync(cancellation: default);
            Assert.True(success);
        }

        [Fact]
        public async Task StoreAsyncTest()
        {
            var database = await GetDatabaseAsync();
            using var scope = database.CreateScope();
            var entry = new MinimalEntry { Id = 1 };

            await scope.StoreAsync(entry, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);
            var roundtripEntry = await database.GetOneAsync<MinimalEntry>(p => p.Id == entry.Id, cancellation: default);

            Assert.True(success);
            Assert.NotNull(roundtripEntry);
        }

        [Fact]
        public async Task StoreAsyncScopeIsolationTest()
        {
            var database = await GetDatabaseAsync();
            using var scope = database.CreateScope();
            var entry = new MinimalEntry { Id = 1 };

            await scope.StoreAsync(entry, cancellation: default);
            var roundtripEntry = await database.GetOneAsync<MinimalEntry>(p => p.Id == entry.Id, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
            Assert.Null(roundtripEntry);
        }

        [Fact]
        public async Task RemoveAsyncTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();
            var entry = new MinimalEntry { Id = 1 };

            await scope.RemoveAsync(entry, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);
            var roundtripEntry = await database.GetOneAsync<MinimalEntry>(p => p.Id == entry.Id, cancellation: default);

            Assert.True(success);
            Assert.Null(roundtripEntry);
        }

        [Fact]
        public async Task RemoveAsyncScopeIsolationTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();
            var entry = new MinimalEntry { Id = 1 };

            await scope.RemoveAsync(entry, cancellation: default);
            var roundtripEntry = await database.GetOneAsync<MinimalEntry>(p => p.Id == entry.Id, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
            Assert.NotNull(roundtripEntry);
        }

        [Fact]
        public async Task GetAsyncTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            var entries = await scope.GetAsync<MinimalEntry>(p => p.Id > 5, cancellation: default).ToListAsync(cancellationToken: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
            Assert.NotNull(entries);
            Assert.Equal(5, entries.Count);
            Assert.True(entries.All(p => Enumerable.Range(6, 5).Contains(p.Id)));
        }

        [Fact]
        public async Task GetAsyncReflectsNonCommitedChangesTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            var entryToRemove = new MinimalEntry { Id = 6 };
            await scope.RemoveAsync(entryToRemove, cancellation: default);

            var entries = await scope.GetAsync<MinimalEntry>(p => p.Id > 5, cancellation: default).ToListAsync(cancellationToken: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
            Assert.NotNull(entries);
            Assert.Equal(4, entries.Count);
            Assert.True(entries.All(p => Enumerable.Range(7, 5).Contains(p.Id)));
        }

        [Fact]
        public async Task UnrepeatableReadTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            await scope.GetAsync<MinimalEntry>(p => p.Id > 5, cancellation: default).ToListAsync(cancellationToken: default);

            var entryToRemove = new MinimalEntry { Id = 6 };
            await database.RemoveAsync(entryToRemove, _ => true, cancellation: default);

            var entries = await scope.GetAsync<MinimalEntry>(p => p.Id > 5, cancellation: default).ToListAsync(cancellationToken: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
            Assert.NotNull(entries);
            Assert.Equal(5, entries.Count);
            Assert.True(entries.All(p => Enumerable.Range(6, 5).Contains(p.Id)));
        }

        [Fact]
        public async Task StoreAsyncLostUpdateViaConcurrentUpdateTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            var entryToUpdate = await scope.GetAsync<MinimalEntry>(p => p.Id == 1, cancellation: default).FirstAsync(cancellationToken: default);
            entryToUpdate.Value = 10;
            await scope.StoreAsync(entryToUpdate, cancellation: default);

            var concurrentlyUpdatedEntry = new MinimalEntry { Id = 1, Value = 5 };
            await database.UpdateAsync(concurrentlyUpdatedEntry, _ => true, cancellation: default);

            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task RemoveAsyncLostUpdateViaConcurrentUpdateTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            var entryToUpdate = await scope.GetAsync<MinimalEntry>(p => p.Id == 1, cancellation: default).FirstAsync(cancellationToken: default);
            entryToUpdate.Value = 10;
            await scope.RemoveAsync(entryToUpdate, cancellation: default);

            var concurrentlyUpdatedEntry = new MinimalEntry { Id = 1, Value = 5 };
            await database.UpdateAsync(concurrentlyUpdatedEntry, _ => true, cancellation: default);

            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task StoreAsyncLostUpdateViaConcurrentRemoveTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            var entryToUpdate = await scope.GetAsync<MinimalEntry>(p => p.Id == 1, cancellation: default).FirstAsync(cancellationToken: default);
            entryToUpdate.Value = 10;
            await scope.StoreAsync(entryToUpdate, cancellation: default);

            var concurrentlyUpdatedEntry = new MinimalEntry { Id = 1, Value = 5 };
            await database.RemoveAsync(concurrentlyUpdatedEntry, _ => true, cancellation: default);

            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task RemoveAsyncLostUpdateViaConcurrentRemoveTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            var entryToUpdate = await scope.GetAsync<MinimalEntry>(p => p.Id == 1, cancellation: default).FirstAsync(cancellationToken: default);
            entryToUpdate.Value = 10;
            await scope.RemoveAsync(entryToUpdate, cancellation: default);

            var concurrentlyUpdatedEntry = new MinimalEntry { Id = 1, Value = 5 };
            await database.RemoveAsync(concurrentlyUpdatedEntry, _ => true, cancellation: default);

            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task RollbackTest()
        {
            var database = await GetDatabaseAsync();
            using var scope = database.CreateScope();
            await scope.StoreAsync(new MinimalEntry { Id = 1 }, cancellation: default);
            await scope.RollbackAsync(cancellation: default);
            var entryCount = await database.GetAsync<MinimalEntry>(cancellation: default).CountAsync(cancellationToken: default);

            Assert.Equal(0, entryCount);
        }

        [Fact]
        public async Task GetAsyncReflectsCurrentStateAfterRestart()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            await scope.GetAsync<MinimalEntry>(p => p.Id > 5, cancellation: default).ToListAsync(cancellationToken: default);

            var entryToRemove = new MinimalEntry { Id = 6 };
            await database.RemoveAsync(entryToRemove, _ => true, cancellation: default);

            await scope.RollbackAsync();

            var entries = await scope.GetAsync<MinimalEntry>(p => p.Id > 5, cancellation: default).ToListAsync(cancellationToken: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
            Assert.NotNull(entries);
            Assert.Equal(4, entries.Count);
            Assert.True(entries.All(p => Enumerable.Range(7, 5).Contains(p.Id)));
        }

        [Fact]
        public async Task StoreAsyncAfterRestartTest()
        {
            var database = await GetDatabaseAsync();
            var entry = new MinimalEntry { Id = 1, Value = 10 };

            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            _ = scope.GetAsync<MinimalEntry>(p => p.Id == 1, cancellation: default);
            await scope.StoreAsync(entry, cancellation: default);
            await database.RemoveAsync(entry, cancellation: default);
            await scope.TryCommitAsync(cancellation: default);


            await scope.StoreAsync(entry, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);
            var roundtripEntry = await database.GetOneAsync<MinimalEntry>(p => p.Id == entry.Id, cancellation: default);

            Assert.True(success);
            Assert.NotNull(roundtripEntry);
            Assert.Equal(entry.Value, roundtripEntry.Value);
        }

        [Fact]
        public async Task RemoveAsyncAfterRestartTest()
        {
            var database = await GetDatabaseAsync();
            var entry = new MinimalEntry { Id = 1 };
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            _ = scope.GetAsync<MinimalEntry>(p => p.Id == 1, cancellation: default);
            await scope.StoreAsync(entry, cancellation: default);
            await database.RemoveAsync(entry, cancellation: default);
            await scope.TryCommitAsync(cancellation: default);

            await scope.RemoveAsync(entry, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);
            var roundtripEntry = await database.GetOneAsync<MinimalEntry>(p => p.Id == entry.Id, cancellation: default);

            Assert.True(success);
            Assert.Null(roundtripEntry);
        }

        [Fact]
        public async Task PhantomReadTest()
        {
            var database = await GetDatabaseAsync();
            await DatabaseSeed.SeedDatabaseAsync(database);
            using var scope = database.CreateScope();

            var entries = await scope.GetAsync<MinimalEntry>(p => true, cancellation: default).ToListAsync();

            using (var updatingScope = database.CreateScope())
            {
                await updatingScope.StoreAsync(new MinimalEntry { Id = 100, Value = 1 }, cancellation: default);
                var updatingValueSumEntry = await updatingScope.GetAsync<ValueSumEntry>(p => true).FirstAsync(cancellationToken: default);
                updatingValueSumEntry.ValueSum += 1;
                await updatingScope.StoreAsync(updatingValueSumEntry, cancellation: default);

                Assert.True(await updatingScope.TryCommitAsync(cancellation: default));
            }

            var valueSumEntry = await scope.GetAsync<ValueSumEntry>(p => true).FirstAsync(cancellationToken: default);

            Assert.Equal(entries.Sum(p => p.Value), valueSumEntry.ValueSum);
        }
    }
}
