using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AI4E.Storage.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed partial class DatabaseScopeExtensionTests
    {
        [Fact]
        public void GetAsyncNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                DatabaseScopeExtension.GetAsync<Entry>(null, cancellation: default);
            });
        }

        [Fact]
        public void GetAsyncTest()
        {
            DatabaseScope.GetResult = AsyncEnumerable.Empty<Entry>();
            var result = DatabaseScopeExtension.GetAsync<Entry>(DatabaseScope, cancellation: default);

            Assert.Same(DatabaseScope.GetResult, result);
        }

        [Fact]
        public async Task GetOnAsyncNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseScopeExtension.GetOneAsync<Entry>(null, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOnAsyncTest()
        {
            var entry = new Entry();
            DatabaseScope.GetResult = new[] { entry }.ToAsyncEnumerable();
            var result = await DatabaseScopeExtension.GetOneAsync<Entry>(DatabaseScope, cancellation: default);

            Assert.Same(entry, result);
        }

        [Fact]
        public async Task GetOnAsyncWithPredicateNullDatabaseScopeThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseScopeExtension.GetOneAsync<Entry>(null, _ => true, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOnAsyncWithPredicateTest()
        {
            var entry = new Entry();
            Expression<Func<Entry, bool>> predicate = _ => true;
            DatabaseScope.GetResult = new[] { entry }.ToAsyncEnumerable();
            var result = await DatabaseScopeExtension.GetOneAsync(DatabaseScope, predicate, cancellation: default);

            Assert.Same(entry, result);
            Assert.Same(predicate, DatabaseScope.Predicate);
        }
    }
}
