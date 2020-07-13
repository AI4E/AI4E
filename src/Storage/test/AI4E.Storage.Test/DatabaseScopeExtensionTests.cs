/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Test.TestTypes;
using Moq;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed class DatabaseScopeExtensionTests
    {
        [Fact]
        public void GetAsyncNullDatabaseScopeThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("databaseScope", () =>
             {
                 DatabaseScopeExtension.GetAsync<Entry>(null, cancellation: default);
             });
        }

        [Fact]
        public void GetAsyncTest()
        {
            // Arrange
            var cancellation = CancellationToken.None;
            var expectedResult = AsyncEnumerable.Empty<Entry>();
            var mock = new Mock<IDatabaseScope>();
            mock.Setup(scope => scope.GetAsync<Entry>(_ => true, cancellation)).Returns(expectedResult);

            // Act
            var result = DatabaseScopeExtension.GetAsync<Entry>(mock.Object, cancellation);

            // Assert
            mock.Verify(scope => scope.GetAsync<Entry>(_ => true, cancellation), Times.Once());
            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task GetOneAsyncNullDatabaseScopeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("databaseScope", async () =>
            {
                await DatabaseScopeExtension.GetOneAsync<Entry>(null, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOneAsyncTest()
        {
            // Arrange
            var cancellation = CancellationToken.None;
            var expectedResult = new Entry();
            var mock = new Mock<IDatabaseScope>();
            mock.Setup(scope => scope.GetAsync<Entry>(_ => true, cancellation))
                .Returns(new[] { expectedResult }.ToAsyncEnumerable());

            mock.Setup(scope => scope.GetOneAsync<Entry>(_ => true, cancellation))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await DatabaseScopeExtension.GetOneAsync<Entry>(mock.Object, cancellation);

            // Assert
            mock.Verify(scope => scope.GetOneAsync<Entry>(_ => true, cancellation), Times.Once());

            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task GetOneAsyncWithPredicateNullDatabaseScopeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("databaseScope", async () =>
            {
                await DatabaseScopeExtension.GetOneAsync<Entry>(null, _ => true, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOneAsyncWithPredicateTest()
        {
            // Arrange
            Expression<Func<Entry, bool>> predicate = p => p.Property == null;
            var cancellation = CancellationToken.None;
            var expectedResult = new Entry();
            var mock = new Mock<IDatabaseScope>();
            mock.Setup(scope => scope.GetAsync(predicate, cancellation))
                .Returns(new[] { expectedResult }.ToAsyncEnumerable());

            // Act
            var result = await DatabaseScopeExtension.GetOneAsync(mock.Object, predicate, cancellation);

            // Assert
            mock.Verify(scope => scope.GetAsync(predicate, cancellation), Times.Once());
            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task StoreAsyncNullDatabaseScopeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("databaseScope", async () =>
            {
                await DatabaseScopeExtension.StoreAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task StoreAsyncTest()
        {
            // Arrange
            var entry = new Entry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabaseScope>();

            // Act
            await DatabaseScopeExtension.StoreAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(scope => scope.StoreAsync(entry, _ => true, cancellation), Times.Once());
        }

        [Fact]
        public async Task RemoveAsyncNullDatabaseScopeThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("databaseScope", async () =>
            {
                await DatabaseScopeExtension.RemoveAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncTest()
        {
            // Arrange
            var entry = new Entry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabaseScope>();

            // Act
            await DatabaseScopeExtension.RemoveAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(scope => scope.RemoveAsync(entry, _ => true, cancellation), Times.Once());
        }
    }
}
