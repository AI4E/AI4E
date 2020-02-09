using System;
using System.Threading.Tasks;
using AI4E.Storage.Test.Mocks;
using AI4E.Storage.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed partial class DatabaseExtensionTests
    {
        private DatabaseMock Database { get; } = new DatabaseMock();

        [Fact]
        public async Task AddAsyncNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.AddAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncWithEntryTypeNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.AddAsync(null, typeof(EntryBase), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.AddAsync(Database, null, cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncWithEntryTypeNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.AddAsync(Database, typeof(EntryBase), null, cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncWithEntryTypeNullEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entryType", async () =>
            {
                await DatabaseExtension.AddAsync(Database, null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncWithEntryTypeNonAssignableEntryTypeThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.AddAsync(Database, typeof(string), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncValueTaskEntryThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.AddAsync(Database, 5, cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncWithEntryTypeValueTaskEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.AddAsync(Database, typeof(int), 5, cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.UpdateAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncWithEntryTypeNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.UpdateAsync(null, typeof(EntryBase), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.UpdateAsync(Database, null, cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncWithEntryTypeNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.UpdateAsync(Database, typeof(EntryBase), null, cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncWithEntryTypeNullEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entryType", async () =>
            {
                await DatabaseExtension.UpdateAsync(Database, null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncWithEntryTypeNonAssignableEntryTypeThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.UpdateAsync(Database, typeof(string), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncValueTaskEntryThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.UpdateAsync(Database, 5, cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncWithEntryTypeValueTaskEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.UpdateAsync(Database, typeof(int), 5, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.RemoveAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.RemoveAsync(null, typeof(EntryBase), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.RemoveAsync(Database, null, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.RemoveAsync(Database, typeof(EntryBase), null, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNullEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entryType", async () =>
            {
                await DatabaseExtension.RemoveAsync(Database, null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNonAssignableEntryTypeThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.RemoveAsync(Database, typeof(string), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncValueTaskEntryThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.RemoveAsync(Database, 5, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeValueTaskEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseExtension.RemoveAsync(Database, typeof(int), 5, cancellation: default);
            });
        }

        [Fact]
        public async Task AddAsyncTest()
        {
            var entry = new Entry();

            await DatabaseExtension.AddAsync(Database, entry, cancellation: default);

            Assert.True(Database.AddCalled);
            Assert.Same(entry, Database.Entry);
            Assert.Equal(entry.GetType(), Database.EntryType);
        }

        [Fact]
        public async Task AddAsyncWithEntryTypeTest()
        {
            var entryType = typeof(EntryBase);
            var entry = new Entry();

            await DatabaseExtension.AddAsync(Database, entryType, entry, cancellation: default);

            Assert.True(Database.AddCalled);
            Assert.Same(entry, Database.Entry);
            Assert.Equal(entryType, Database.EntryType);
        }

        [Fact]
        public async Task UpdateAsyncTest()
        {
            var entry = new Entry();

            await DatabaseExtension.UpdateAsync(Database, entry, cancellation: default);

            Assert.True(Database.UpdateCalled);
            Assert.Same(entry, Database.Entry);
            Assert.Equal(entry.GetType(), Database.EntryType);
        }

        [Fact]
        public async Task UpdateAsyncWithEntryTypeTest()
        {
            var entryType = typeof(EntryBase);
            var entry = new Entry();

            await DatabaseExtension.UpdateAsync(Database, entryType, entry, cancellation: default);

            Assert.True(Database.UpdateCalled);
            Assert.Same(entry, Database.Entry);
            Assert.Equal(entryType, Database.EntryType);
        }

        [Fact]
        public async Task RemoveAsyncTest()
        {
            var entry = new Entry();

            await DatabaseExtension.RemoveAsync(Database, entry, cancellation: default);

            Assert.True(Database.RemoveCalled);
            Assert.Same(entry, Database.Entry);
            Assert.Equal(entry.GetType(), Database.EntryType);
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeTest()
        {
            var entryType = typeof(EntryBase);
            var entry = new Entry();

            await DatabaseExtension.RemoveAsync(Database, entryType, entry, cancellation: default);

            Assert.True(Database.RemoveCalled);
            Assert.Same(entry, Database.Entry);
            Assert.Equal(entryType, Database.EntryType);
        }
    }
}
