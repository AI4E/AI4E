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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Test.TestTypes;
using Moq;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed partial class DatabaseExtensionTests
    {
        [Fact]
        public void GetAsyncNullDatabaseThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("database", () =>
            {
                DatabaseExtension.GetAsync<Entry>(null, cancellation: default);
            });
        }

        [Fact]
        public void GetAsyncTest()
        {
            // Arrange
            var cancellation = CancellationToken.None;
            var expectedResult = AsyncEnumerable.Empty<Entry>();
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.GetAsync<Entry>(_ => true, cancellation)).Returns(expectedResult);

            // Act
            var result = DatabaseExtension.GetAsync<Entry>(mock.Object, cancellation);

            // Assert
            mock.Verify(database => database.GetAsync<Entry>(_ => true, cancellation), Times.Once());
            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task GetOneAsyncNullDatabaseThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("database", async () =>
            {
                await DatabaseExtension.GetOneAsync<Entry>(null, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOneAsyncTest()
        {
            // Arrange
            var cancellation = CancellationToken.None;
            var expectedResult = new Entry();
            var mock = new Mock<IDatabase>();

            mock.Setup(database => database.GetOneAsync<Entry>(_ => true, cancellation))
                .ReturnsAsync(expectedResult);
            // Act
            var result = await DatabaseExtension.GetOneAsync<Entry>(mock.Object, cancellation);

            // Assert
            mock.Verify(database => database.GetOneAsync<Entry>(_ => true, cancellation), Times.Once());
            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task GetOneAsyncWithPredicateNullDatabaseThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("database", async () =>
            {
                await DatabaseExtension.GetOneAsync<Entry>(null, _ => true, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOneAsyncWithPredicateTest()
        {
            // Arrange
            Expression<Func<Entry, bool>> predicate = p => p.Property == null;
            var cancellation = CancellationToken.None;
            var expectedResult = new Entry();
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.GetAsync(predicate, cancellation))
                .Returns(new[] { expectedResult }.ToAsyncEnumerable());

            // Act
            var result = await DatabaseExtension.GetOneAsync(mock.Object, predicate, cancellation);

            // Assert
            mock.Verify(database => database.GetAsync(predicate, cancellation), Times.Once());
            Assert.Same(expectedResult, result);
        }

        [Fact]
        public async Task UpdateAsyncNullDatabaseThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("database", async () =>
            {
                await DatabaseExtension.UpdateAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task UpdateAsyncTest()
        {
            // Arrange
            var entry = new Entry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            // Act
            await DatabaseExtension.UpdateAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(database => database.UpdateAsync(entry, _ => true, cancellation), Times.Once());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UpdateAsyncReturnsCorrectResultTest(bool success)
        {
            // Arrange
            var entry = new Entry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.UpdateAsync(entry, _ => true, cancellation)).ReturnsAsync(success);

            // Act
            var result = await DatabaseExtension.UpdateAsync(mock.Object, entry, cancellation);

            // Assert
            Assert.Equal(success, result);
        }

        [Fact]
        public async Task RemoveAsyncNullDatabaseThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("database", async () =>
            {
                await DatabaseExtension.RemoveAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task RemoveAsyncTest()
        {
            // Arrange
            var entry = new Entry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            // Act
            await DatabaseExtension.RemoveAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(database => database.RemoveAsync(entry, _ => true, cancellation), Times.Once());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RemoveAsyncReturnsCorrectResultTest(bool success)
        {
            // Arrange
            var entry = new Entry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.RemoveAsync(entry, _ => true, cancellation)).ReturnsAsync(success);

            // Act
            var result = await DatabaseExtension.RemoveAsync(mock.Object, entry, cancellation);

            // Assert
            Assert.Equal(success, result);
        }

        [Fact]
        public async Task GetOrAddAsyncNullDatabaseThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("database", async () =>
            {
                await DatabaseExtension.GetOrAddAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task GetOrAddAsyncNullEntryThrowsArgumentNullExceptionTest()
        {
            var mock = new Mock<IDatabase>();

            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.GetOrAddAsync<Entry>(mock.Object, null, cancellation: default);
            });
        }

        [Fact]
        public async Task GetOrAddAsyncNoIdEntryIsAddedTest()
        {
            // Arrange
            var entry = new NoIdEntry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.AddAsync(entry, cancellation)).ReturnsAsync(true);

            // Act
            await DatabaseExtension.GetOrAddAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(database => database.AddAsync(entry, cancellation), Times.Once());
        }

        [Fact]
        public async Task GetOrAddAsyncNoIdEntryReturnsEntryTest()
        {
            // Arrange
            var entry = new NoIdEntry();
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.AddAsync(entry, cancellation)).ReturnsAsync(true);

            // Act
            var resultEntry = await DatabaseExtension.GetOrAddAsync(mock.Object, entry, cancellation);

            // Assert
            Assert.Same(entry, resultEntry);
        }

        [Fact]
        public async Task GetOrAddAsyncNonExistingEntryIsAddedTest()
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.AddAsync(entry, cancellation)).ReturnsAsync(true);

            // Act
            await DatabaseExtension.GetOrAddAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(database => database.AddAsync(entry, cancellation), Times.Once());
            mock.Verify(database => database.GetOneAsync<IdEntry>(p => p.Id == 1, cancellation), Times.Never());
        }

        [Fact]
        public async Task GetOrAddAsyncNonExistingEntryReturnsEntryTest()
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.AddAsync(entry, cancellation)).ReturnsAsync(true);

            // Act
            var resultEntry = await DatabaseExtension.GetOrAddAsync(mock.Object, entry, cancellation);

            // Assert
            Assert.Same(entry, resultEntry);
        }

        [Fact]
        public async Task GetOrAddAsyncExistingEntryIsRetrievedTest()
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var exisintgEntry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            mock.Setup(database => database.GetOneAsync<IdEntry>(
                It.IsAny<Expression<Func<IdEntry, bool>>>(),
                cancellation)).ReturnsAsync(exisintgEntry);

            // Act
            await DatabaseExtension.GetOrAddAsync(mock.Object, entry, cancellation);

            // Assert
            // TODO: Use a custom equality comparer, when this is implemented (#252) to validate the predicate
            mock.Verify(database => database.GetOneAsync<IdEntry>(
            It.IsAny<Expression<Func<IdEntry, bool>>>(),
            cancellation), Times.Once());
        }

        [Fact]
        public async Task GetOrAddAsyncExistingEntryReturnsExistingEntryTest()
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var exisintgEntry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            // TODO: Use a custom equality comparer, when this is implemented (#252) to validate the predicate
            mock.Setup(database => database.GetOneAsync<IdEntry>(
                It.IsAny<Expression<Func<IdEntry, bool>>>(),
                cancellation)).ReturnsAsync(exisintgEntry);

            // Act
            var resultEntry = await DatabaseExtension.GetOrAddAsync(mock.Object, entry, cancellation);

            // Assert
            Assert.Same(exisintgEntry, resultEntry);
        }

        [Fact]
        public async Task AddOrUpdateAsyncNullDatabaseThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("database", async () =>
            {
                await DatabaseExtension.AddOrUpdateAsync(null, new Entry(), cancellation: default);
            });
        }

        [Fact]
        public async Task AddOrUpdateAsyncNullEntryThrowsArgumentNullExceptionTest()
        {
            var mock = new Mock<IDatabase>();

            await Assert.ThrowsAsync<ArgumentNullException>("entry", async () =>
            {
                await DatabaseExtension.AddOrUpdateAsync<Entry>(mock.Object, null, cancellation: default);
            });
        }

        [Fact]
        public async Task AddOrUpdateAsyncNonExistingEntryIsAddedTest()
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.AddAsync(entry, cancellation)).ReturnsAsync(true);

            // Act
            await DatabaseExtension.AddOrUpdateAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(database => database.AddAsync(entry, cancellation), Times.Once());
            mock.Verify(database => database.UpdateAsync(entry, _ => true, cancellation), Times.Never());
        }

        [Fact]
        public async Task AddOrUpdateAsyncExistingEntryIsUpdatedTest()
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.AddAsync(entry, cancellation)).ReturnsAsync(false);
            mock.Setup(database => database.UpdateAsync(entry, _ => true, cancellation)).ReturnsAsync(true);

            // Act
            await DatabaseExtension.AddOrUpdateAsync(mock.Object, entry, cancellation);

            // Assert
            mock.Verify(database => database.UpdateAsync(entry, _ => true, cancellation), Times.Once());
        }

        [Fact]
        public async Task CompareExchangeAsyncNullDatabaseThrowsArgumentNullExceptionTest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>("database", async () =>
            {
                await DatabaseExtension.CompareExchangeAsync<IdEntry>(
                    database: null,
                    entry: null,
                    comparand: null,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation: default);
            });
        }

        [Fact]
        public async Task CompareExchangeAsyncNullEntrySelectorThrowsArgumentNullExceptionTest()
        {
            var mock = new Mock<IDatabase>();

            await Assert.ThrowsAsync<ArgumentNullException>("entrySelector", async () =>
            {
                await DatabaseExtension.CompareExchangeAsync<IdEntry>(
                    database: mock.Object,
                    entry: null,
                    comparand: null,
                    entrySelector: null,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation: default);
            });
        }

        [Fact]
        public async Task CompareExchangeAsyncNullEqualityComparerThrowsArgumentNullExceptionTest()
        {
            var mock = new Mock<IDatabase>();

            await Assert.ThrowsAsync<ArgumentNullException>("equalityComparer", async () =>
            {
                await DatabaseExtension.CompareExchangeAsync<IdEntry>(
                    database: mock.Object,
                    entry: null,
                    comparand: null,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: null,
                    cancellation: default);
            });
        }

        [Fact]
        public async Task CompareExchangeAsyncEntryDoesNotMatchEntrySelectorThrowsArgumentExceptionTest()
        {
            var mock = new Mock<IDatabase>();

            await Assert.ThrowsAsync<ArgumentException>("entry", async () =>
            {
                await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry: new IdEntry { Id = 2 },
                    comparand: null,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation: default);
            });
        }

        [Fact]
        public async Task CompareExchangeAsyncComparandDoesNotMatchEntrySelectorThrowsArgumentExceptionTest()
        {
            var mock = new Mock<IDatabase>();

            await Assert.ThrowsAsync<ArgumentException>("comparand", async () =>
            {
                await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry: null,
                    comparand: new IdEntry { Id = 2 },
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation: default);
            });
        }

        [Theory]
        [ClassData(typeof(CompareExchangeAsyncEqualEntryComparandChecksToBeUpToDateTestData))]
        public async Task CompareExchangeAsyncEqualEntryComparandChecksToBeUpToDateTest(
            IdEntry? entry,
            IdEntry existingEntry,
            Expression<Func<IdEntry, IdEntry, bool>>? equalityComparer)
        {
            // Arrange
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            mock.Setup(database => database.GetOneAsync<IdEntry>(
                It.IsAny<Expression<Func<IdEntry, bool>>>(),
                cancellation)).ReturnsAsync(existingEntry);

            // Act
            await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry: entry,
                    comparand: entry,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: equalityComparer ?? ((x, y) => x.Id == y.Id),
                    cancellation);

            // Assert
            // TODO: Use a custom equality comparer, when this is implemented (#252) to validate the predicate
            mock.Verify(
                database => database.GetOneAsync<IdEntry>(It.IsAny<Expression<Func<IdEntry, bool>>>(), cancellation),
                Times.Once());
        }

        [Theory]
        [ClassData(typeof(CompareExchangeAsyncEqualEntryComparandReturnsCorrectResultTestData))]
        public async Task CompareExchangeAsyncEqualEntryComparandReturnsCorrectResultTest(
            IdEntry? entry,
            IdEntry existingEntry,
            Expression<Func<IdEntry, IdEntry, bool>>? equalityComparer,
            bool expectedResult)
        {
            // Arrange
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            mock.Setup(database => database.GetOneAsync<IdEntry>(
                It.IsAny<Expression<Func<IdEntry, bool>>>(),
                cancellation)).ReturnsAsync(existingEntry);

            // Act
            var result = await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry: entry,
                    comparand: entry,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: equalityComparer ?? ((x, y) => x.Id == y.Id),
                    cancellation);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task CompareExchangeAsyncNullComparandEntryIsAddedTest()
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            // Act
            await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry,
                    comparand: null,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation);

            // Assert
            mock.Verify(database => database.AddAsync(entry, cancellation), Times.Once());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CompareExchangeAsyncNullComparandReturnsCorrectResultTest(bool expectedResult)
        {
            // Arrange
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();
            mock.Setup(database => database.AddAsync(entry, cancellation)).ReturnsAsync(expectedResult);

            // Act
            var result = await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry,
                    comparand: null,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task CompareExchangeAsyncNullEntryIsRemovedTest()
        {
            // Arrange
            var comparand = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            // Act
            await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry: null,
                    comparand,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation);

            // Assert
            // TODO: Use a custom equality comparer, when this is implemented (#252) to validate the predicate
            mock.Verify(database => database.RemoveAsync(comparand, It.IsAny<Expression<Func<IdEntry, bool>>>(), cancellation), Times.Once());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CompareExchangeAsyncNullEntryReturnsCorrectResultTest(bool expectedResult)
        {
            // Arrange
            var comparand = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            mock.Setup(database => database.RemoveAsync(
                comparand, It.IsAny<Expression<Func<IdEntry, bool>>>(),
                cancellation)).ReturnsAsync(expectedResult);

            // Act
            var result = await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry: null,
                    comparand,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task CompareExchangeEntryIsUpdatedTest()
        {
            // Arrange
            var comparand = new IdEntry { Id = 1 };
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            // Act
            await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry,
                    comparand,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation);

            // Assert
            // TODO: Use a custom equality comparer, when this is implemented (#252) to validate the predicate
            mock.Verify(database => database.UpdateAsync(entry, It.IsAny<Expression<Func<IdEntry, bool>>>(), cancellation), Times.Once());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CompareExchangeReturnsCorrectResultTest(bool expectedResult)
        {
            // Arrange
            var comparand = new IdEntry { Id = 1 };
            var entry = new IdEntry { Id = 1 };
            var cancellation = CancellationToken.None;
            var mock = new Mock<IDatabase>();

            mock.Setup(database => database.UpdateAsync(
                entry,
                It.IsAny<Expression<Func<IdEntry, bool>>>(),
                cancellation)).ReturnsAsync(expectedResult);

            // Act
            var result = await DatabaseExtension.CompareExchangeAsync(
                    database: mock.Object,
                    entry,
                    comparand,
                    entrySelector: p => p.Id == 1,
                    equalityComparer: (x, y) => x.Id == y.Id,
                    cancellation);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        public class CompareExchangeAsyncEqualEntryComparandChecksToBeUpToDateTestData
            : TheoryData<IdEntry?, IdEntry, Expression<Func<IdEntry, IdEntry, bool>>?>
        {
            public CompareExchangeAsyncEqualEntryComparandChecksToBeUpToDateTestData()
            {
                Add(null, null, null);
                Add(null, new IdEntry { Id = 1 }, null);
                Add(new IdEntry { Id = 1 }, null, null);
                Add(new IdEntry { Id = 1 }, new IdEntry { Id = 1 }, (x, y) => false);
                Add(new IdEntry { Id = 1 }, new IdEntry { Id = 1 }, (x, y) => true);
            }
        }

        public class CompareExchangeAsyncEqualEntryComparandReturnsCorrectResultTestData
            : TheoryData<IdEntry?, IdEntry, Expression<Func<IdEntry, IdEntry, bool>>?, bool>
        {
            public CompareExchangeAsyncEqualEntryComparandReturnsCorrectResultTestData()
            {
                Add(null, null, null, true);
                Add(null, new IdEntry { Id = 1 }, null, false);
                Add(new IdEntry { Id = 1 }, null, null, false);
                Add(new IdEntry { Id = 1 }, new IdEntry { Id = 1 }, (x, y) => false, false);
                Add(new IdEntry { Id = 1 }, new IdEntry { Id = 1 }, (x, y) => true, true);
            }
        }
    }
}
