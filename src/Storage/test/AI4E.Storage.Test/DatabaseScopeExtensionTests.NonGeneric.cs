using System;
using System.Threading.Tasks;
using AI4E.Storage.Test.Mocks;
using AI4E.Storage.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed partial class DatabaseScopeExtensionTests
    {
        private DatabaseScopeMock DatabaseScope { get; } = new DatabaseScopeMock();

        [Fact]
        public async Task StoreAsyncNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseScopeExtension.StoreAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncWithEntryTypeNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseScopeExtension.StoreAsync(null, typeof(EntryBase), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseScopeExtension.StoreAsync(DatabaseScope, null, cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncWithEntryTypeNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseScopeExtension.StoreAsync(DatabaseScope, typeof(EntryBase), null, cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncWithEntryTypeNullEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entryType", async () =>
            {
                await DatabaseScopeExtension.StoreAsync(DatabaseScope, null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncWithEntryTypeNonAssignableEntryTypeThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseScopeExtension.StoreAsync(DatabaseScope, typeof(string), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncValueTaskEntryThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseScopeExtension.StoreAsync(DatabaseScope, 5, cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncWithEntryTypeValueTaskEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseScopeExtension.StoreAsync(DatabaseScope, typeof(int), 5, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(null, typeof(EntryBase), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(DatabaseScope, null, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNullEntryThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(DatabaseScope, typeof(EntryBase), null, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNullEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("entryType", async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(DatabaseScope, null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeNonAssignableEntryTypeThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(DatabaseScope, typeof(string), new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncValueTaskEntryThrowsArgumentExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(DatabaseScope, 5, cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeValueTaskEntryTypeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(DatabaseScope, typeof(int), 5, cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncTest()
        {
            var entry = new Entry();

            await DatabaseScopeExtension.StoreAsync(DatabaseScope, entry, cancellation: default);

            Assert.True(DatabaseScope.StoreCalled);
            Assert.Same(entry, DatabaseScope.Entry);
            Assert.Equal(entry.GetType(), DatabaseScope.EntryType);
        }

        [Fact]
        public async Task StoreAsyncWithEntryTypeTest()
        {
            var entryType = typeof(EntryBase);
            var entry = new Entry();

            await DatabaseScopeExtension.StoreAsync(DatabaseScope, entryType, entry, cancellation: default);

            Assert.True(DatabaseScope.StoreCalled);
            Assert.Same(entry, DatabaseScope.Entry);
            Assert.Equal(entryType, DatabaseScope.EntryType);
        }

        [Fact]
        public async Task RemoveAsyncTest()
        {
            var entry = new Entry();

            await DatabaseScopeExtension.RemoveAsync(DatabaseScope, entry, cancellation: default);

            Assert.True(DatabaseScope.RemoveCalled);
            Assert.Same(entry, DatabaseScope.Entry);
            Assert.Equal(entry.GetType(), DatabaseScope.EntryType);
        }

        [Fact]
        public async Task RemoveAsyncWithEntryTypeTest()
        {
            var entryType = typeof(EntryBase);
            var entry = new Entry();

            await DatabaseScopeExtension.RemoveAsync(DatabaseScope, entryType, entry, cancellation: default);

            Assert.True(DatabaseScope.RemoveCalled);
            Assert.Same(entry, DatabaseScope.Entry);
            Assert.Equal(entryType, DatabaseScope.EntryType);
        }
    }
}
