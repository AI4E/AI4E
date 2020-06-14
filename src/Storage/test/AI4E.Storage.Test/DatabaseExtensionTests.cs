using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AI4E.Storage.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed partial class DatabaseExtensionTests
    {
        [Fact]
        public void GetAsyncNullDatabaseThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                DatabaseExtension.GetAsync<Entry>(null, cancellation: default);
            });
        }

        [Fact]
        public void GetAsyncTest()
        {
            Database.GetResult = AsyncEnumerable.Empty<Entry>();
            var result = DatabaseExtension.GetAsync<Entry>(Database, cancellation: default);

            Assert.Same(Database.GetResult, result);
        }

        [Fact]
        public async Task GetOnAsyncNullDatabaseThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.GetOneAsync<Entry>(null, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOnAsyncTest()
        {
            var entry = new Entry();
            Database.GetResult = new[] { entry }.ToAsyncEnumerable();
            var result = await DatabaseExtension.GetOneAsync<Entry>(Database, cancellation: default);

            Assert.Same(entry, result);
        }

        [Fact]
        public async Task GetOnAsyncWithPredicateNullDatabaseThrowsNullReferenceExceptionTest()
        {
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await DatabaseExtension.GetOneAsync<Entry>(null, _ => true, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOnAsyncWithPredicateTest()
        {
            var entry = new Entry();
            Expression<Func<Entry, bool>> predicate = _ => true;
            Database.GetResult = new[] { entry }.ToAsyncEnumerable();
            var result = await DatabaseExtension.GetOneAsync(Database, predicate, cancellation: default);

            Assert.Same(entry, result);
            Assert.Same(predicate, Database.Predicate);
        }
    }
}
