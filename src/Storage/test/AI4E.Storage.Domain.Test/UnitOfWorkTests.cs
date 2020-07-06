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
using System.Threading.Tasks;
using AI4E.Storage.Domain.Specification.TestTypes;
using Moq;
using Xunit;
using UnitOfWork = AI4E.Storage.Domain.Tracking.UnitOfWork<AI4E.Storage.Domain.IEntityLoadResult>;
using CommitAttemptEntry = AI4E.Storage.Domain.Tracking.CommitAttemptEntry<AI4E.Storage.Domain.IEntityLoadResult>;
using System.Linq;
using System.Threading;

namespace AI4E.Storage.Domain.Test
{
    public sealed class UnitOfWorkTests
    {
        private static readonly IConcurrencyTokenFactory _concurrencyTokenFactoryFake
            = new Mock<IConcurrencyTokenFactory>().Object;

        private static IConcurrencyTokenFactory CreateConcurrencyTokenFactory(ConcurrencyToken concurrencyToken)
        {
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(concurrencyToken);

            return concurrencyTokenFactoryMock.Object;
        }

        private static readonly EntityIdentifier EntityIdentifier1 = new EntityIdentifier(typeof(DomainEntityBase), "abc");
        private static readonly EntityIdentifier EntityIdentifier2 = new EntityIdentifier(typeof(DomainEntity1), "abc");
        private static readonly EntityIdentifier EntityIdentifier3 = new EntityIdentifier(typeof(DomainEntity2), "def");
        private static readonly EntityIdentifier EntityIdentifier4 = new EntityIdentifier(typeof(DomainEntity2), "xx");
        private static readonly EntityIdentifier EntityIdentifier5 = new EntityIdentifier(typeof(object), "xx");

        private static readonly EntityQueryResult EntityLoadResult1 = new FoundEntityQueryResult(
            EntityIdentifier1,
            new DomainEntity2(),
            new ConcurrencyToken("hhh"),
            revision: 22,
            loadedFromCache: true,
            scope: EntityQueryResultGlobalScope.Instance);

        private static readonly EntityQueryResult EntityLoadResult1_Alternative = new NotFoundEntityQueryResult(
            EntityIdentifier1,
            loadedFromCache: false,
            scope: EntityQueryResultGlobalScope.Instance);

        private static readonly EntityQueryResult EntityLoadResult2 = new NotFoundEntityQueryResult(
          EntityIdentifier2,
          loadedFromCache: true,
          scope: EntityQueryResultGlobalScope.Instance);

        private static readonly EntityQueryResult EntityLoadResult3 = new FoundEntityQueryResult(
           EntityIdentifier3,
           new DomainEntity2(),
           new ConcurrencyToken("hhh"),
           revision: 22,
           loadedFromCache: false,
           scope: EntityQueryResultGlobalScope.Instance);

        private static readonly EntityQueryResult EntityLoadResult4 = new NotFoundEntityQueryResult(
            EntityIdentifier4,
            loadedFromCache: true,
            scope: EntityQueryResultGlobalScope.Instance);

        private static readonly EntityQueryResult EntityLoadResult5 = new NotFoundEntityQueryResult(
            EntityIdentifier5,
            loadedFromCache: true,
            scope: EntityQueryResultGlobalScope.Instance);

        private static readonly DomainEventCollection DomainEventCollection1
            = new DomainEventCollection(new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(object), new DomainEvent1())
            });

        private static readonly DomainEventCollection DomainEventCollection2
            = new DomainEventCollection(new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEvent1), new DomainEvent1()),
                new DomainEvent(typeof(DomainEvent2), new DomainEvent2())
            });

        private static readonly DomainEventCollection CombinedDomainEventCollection
           = DomainEventCollection1.Concat(DomainEventCollection2);

        private static UnitOfWork CreateUnitOfWork()
        {
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);

            unitOfWork.GetOrUpdate(EntityLoadResult1);
            unitOfWork.GetOrUpdate(EntityLoadResult2);
            unitOfWork.GetOrUpdate(EntityLoadResult3);

            return unitOfWork;
        }

        [Fact]
        public void CreateNullConcurrencyTokenFactoryThrowsArgumentNullException()
        {
            // Arrange
            // -

            // Act
            static void Act()
            {
                new UnitOfWork(concurrencyTokenFactory: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("concurrencyTokenFactory", Act);
        }

        [Fact]
        public void EntriesOnNewInstanceYieldNoResultTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);

            // Act
            var entries = subject.Entries;

            // Assert
            Assert.Empty(entries);
        }

        [Fact]
        public void TryGetEntryOnNewInstanceYieldsNoResultTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);

            // Act
            var entryFound = subject.TryGetEntry(EntityIdentifier1, out _);

            // Assert
            Assert.False(entryFound);
        }

        [Fact]
        public void TryGetEntryReturnsTrueOnExistingEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);
            subject.GetOrUpdate(EntityLoadResult1);

            // Act
            var isFound = subject.TryGetEntry(EntityIdentifier1, out _);

            // Assert
            Assert.True(isFound);
        }

        [Fact]
        public void TryGetEntryReturnsYieldsEntryOnExistingEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);
            var expectedEntry = subject.GetOrUpdate(EntityLoadResult1);

            // Act
            subject.TryGetEntry(EntityIdentifier1, out var entry);

            // Assert
            Assert.Same(expectedEntry, entry);
        }

        [Fact]
        public void GetOrUpdateNullEntityLoadResultThrowsArgumentNullExceptionTest()
        {
            // Arrange 
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);

            // Act
            void Act()
            {
                subject.GetOrUpdate(entityLoadResult: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("entityLoadResult", Act);
        }

        [Fact]
        public void GetOrUpdateOnNewInstanceCreatesEntryWithCorrectUnitOfWorkTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);

            // Act
            var entry = subject.GetOrUpdate(EntityLoadResult1);

            // Assert
            Assert.Same(subject, entry.UnitOfWork);
        }

        [Fact]
        public void GetOrUpdateOnNewInstanceCreatesEntryWithCorrectEntityLoadResultTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);

            // Act
            var entry = subject.GetOrUpdate(EntityLoadResult1);

            // Assert
            Assert.Same(EntityLoadResult1, entry.EntityLoadResult.TrackedLoadResult);
        }

        [Fact]
        public void GetOrUpdateOnNewInstanceCreatesNonModifiedEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);

            // Act
            var entry = subject.GetOrUpdate(EntityLoadResult1);

            // Assert
            Assert.False(entry.IsModified);
        }

        [Fact]
        public void GetOrUpdateOnNewInstanceCreatesEntryWithNoRecordedDomainEventsTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);

            // Act
            var entry = subject.GetOrUpdate(EntityLoadResult1);

            // Assert
            Assert.Empty(entry.RecordedDomainEvents);
        }

        [Fact]
        public void GetOrUpdateOnExistingEntryReturnsEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(_concurrencyTokenFactoryFake);
            var expectedEntry = subject.GetOrUpdate(EntityLoadResult1);

            // Act
            var entry = subject.GetOrUpdate(EntityLoadResult1_Alternative);

            // Assert
            Assert.Same(expectedEntry, entry);
        }

        [Fact]
        public void EntriesOnResetInstanceYieldNoResultTest()
        {
            // Arrange
            var subject = CreateUnitOfWork();
            subject.Reset();

            // Act
            var entries = subject.Entries;

            // Assert
            Assert.Empty(entries);
        }

        [Fact]
        public void TryGetEntryOnResetInstanceYieldsNoResultTest()
        {
            // Arrange
            var subject = CreateUnitOfWork();
            subject.Reset();

            // Act
            var entryFound = subject.TryGetEntry(EntityIdentifier1, out _);

            // Assert
            Assert.False(entryFound);
        }

        [Fact]
        public void DeleteEntryIsUpdatedInUnitOfWorkTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);

            // Act
            var deletedEntry = entry.Delete(default);

            // Assert
            Assert.Same(deletedEntry, unitOfWork.GetOrUpdate(EntityLoadResult1));
        }

        [Fact]
        public void DeleteEntryInResetUnitOfWorkIsNotUpdatedInUnitOfWorkTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);

            unitOfWork.Reset();

            // Act
            entry.Delete(default);

            // Assert
            Assert.False(unitOfWork.TryGetEntry(EntityIdentifier1, out _));
        }

        [Fact]
        public void DeleteEntryIsModifiedTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);

            // Act
            var deletedEntry = entry.Delete(default);

            // Assert
            Assert.True(deletedEntry.IsModified);
        }

        [Fact]
        public void DeleteEntryDeletesEntityTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);

            // Act
            var deletedEntry = entry.Delete(default);

            // Assert
            Assert.Null(deletedEntry.EntityLoadResult.GetEntity(throwOnFailure: false));
        }

        [Fact]
        public void DeleteEntryRecordsDomainEventsTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);

            // Act
            var deletedEntry = entry.Delete(DomainEventCollection1);

            // Assert
            Assert.Equal(DomainEventCollection1, deletedEntry.RecordedDomainEvents);
        }

        [Fact]
        public void DeleteEntryCombinesDomainEventsTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            entry = entry.Delete(DomainEventCollection2);


            // Act
            var deletedEntry = entry.Delete(DomainEventCollection1);

            // Assert
            Assert.Equal(CombinedDomainEventCollection, deletedEntry.RecordedDomainEvents);
        }

        [Fact]
        public void DeleteEntryDoesNothingWhenNotNecessaryTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            entry = entry.Delete(DomainEventCollection2);

            // Act
            var deletedEntry = entry.Delete(DomainEventCollection2);

            // Assert
            Assert.Same(entry, deletedEntry);
        }

        [Fact]
        public void CreateOrUpdateEntryIsUpdatedInUnitOfWorkTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            var entity = new DomainEntity1();

            // Act
            var deletedEntry = entry.CreateOrUpdate(entity, default);

            // Assert
            Assert.Same(deletedEntry, unitOfWork.GetOrUpdate(EntityLoadResult1));
        }

        [Fact]
        public void CreateOrUpdateEntryInResetUnitOfWorkIsNotUpdatedInUnitOfWorkTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            var entity = new DomainEntity1();

            unitOfWork.Reset();

            // Act
            entry.CreateOrUpdate(entity, default);

            // Assert
            Assert.False(unitOfWork.TryGetEntry(EntityIdentifier1, out _));
        }

        [Fact]
        public void CreateOrUpdateEntryIsModifiedTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            var entity = new DomainEntity1();

            // Act
            var deletedEntry = entry.CreateOrUpdate(entity, default);

            // Assert
            Assert.True(deletedEntry.IsModified);
        }

        [Fact]
        public void CreateOrUpdateEntryCreatesEntityTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            var entity = new DomainEntity1();

            // Act
            var deletedEntry = entry.CreateOrUpdate(entity, default);

            // Assert
            Assert.Same(entity, deletedEntry.EntityLoadResult.GetEntity(throwOnFailure: false));
        }

        [Fact]
        public void CreateOrUpdateEntryRecordsDomainEventsTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            var entity = new DomainEntity1();

            // Act
            var deletedEntry = entry.CreateOrUpdate(entity, DomainEventCollection1);

            // Assert
            Assert.Equal(DomainEventCollection1, deletedEntry.RecordedDomainEvents);
        }

        [Fact]
        public void CreateOrUpdateEntryCombinesDomainEventsTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            entry = entry.Delete(DomainEventCollection2);
            var entity = new DomainEntity1();

            // Act
            var deletedEntry = entry.CreateOrUpdate(entity, DomainEventCollection1);

            // Assert
            Assert.Equal(CombinedDomainEventCollection, deletedEntry.RecordedDomainEvents);
        }

        [Fact]
        public void CreateOrUpdateEntryDoesNothingWhenNotNecessaryTest()
        {
            // Arrange
            var unitOfWork = new UnitOfWork(_concurrencyTokenFactoryFake);
            var entry = unitOfWork.GetOrUpdate(EntityLoadResult1);
            var entity = new DomainEntity1();
            entry = entry.CreateOrUpdate(entity, DomainEventCollection2);

            // Act
            var deletedEntry = entry.CreateOrUpdate(entity, DomainEventCollection2);

            // Assert
            Assert.Same(entry, deletedEntry);
        }

        [Fact]
        public async Task CommitAsyncNullStorageEngineThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var subject = CreateUnitOfWork();

            // Act
            async Task ActAsync()
            {
                await subject.CommitAsync(storageEngine: null);
            }

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>("storageEngine", ActAsync);
        }

        [Fact]
        public async Task CommitAsyncResetsUnitOfWorkTest()
        {
            // Arrange
            var subject = CreateUnitOfWork();

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            await subject.CommitAsync(storageEngine);

            // Act
            var entries = subject.Entries;

            // Assert
            Assert.Empty(entries);
        }

        [Theory]
        [InlineData(EntityCommitResult.Success)]
        [InlineData(EntityCommitResult.ConcurrencyFailure)]
        public async Task CommitAsyncPassesCorrectResultTest(EntityCommitResult expectedCommitResult)
        {
            // Arrange
            var subject = CreateUnitOfWork();

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .ReturnsAsync(expectedCommitResult);
            var storageEngine = storageEngineMock.Object;

            // Act
            var commitResult = await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(expectedCommitResult, commitResult);
        }

        [Fact]
        public async Task CommitAsyncNumberOfEntriesTest()
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));

            subject.GetOrUpdate(EntityLoadResult1).Delete(DomainEventCollection1);
            subject.GetOrUpdate(EntityLoadResult2).CreateOrUpdate(new DomainEntity1(), DomainEventCollection1);
            subject.GetOrUpdate(EntityLoadResult3).CreateOrUpdate(new DomainEntity2(), DomainEventCollection1);
            subject.GetOrUpdate(EntityLoadResult4).Delete(DomainEventCollection1);
            subject.GetOrUpdate(EntityLoadResult5);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(4, storedCommitAttempt.Entries.Count);
        }

        [Fact]
        public async Task CommitAsyncSetsCorrectEntityIdentifierToCommitAttemptEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            subject.GetOrUpdate(EntityLoadResult1).Delete(DomainEventCollection1);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(EntityIdentifier1, storedCommitAttempt.Entries.First().EntityIdentifier);
        }

        [Theory]
        [ClassData(typeof(CommitAsyncSetsCorrectOperationToCommitAttemptEntryTestData))]
        public async Task CommitAsyncSetsCorrectOperationToCommitAttemptEntryTest(
            Action<UnitOfWork> entryShaper,
            CommitOperation commitOperation)
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            entryShaper(subject);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(commitOperation, storedCommitAttempt.Entries.First().Operation);
        }

        public class CommitAsyncSetsCorrectOperationToCommitAttemptEntryTestData
            : TheoryData<Action<UnitOfWork>, CommitOperation>
        {
            public CommitAsyncSetsCorrectOperationToCommitAttemptEntryTestData()
            {
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult1).Delete(DomainEventCollection1), CommitOperation.Delete);
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult2).CreateOrUpdate(new DomainEntity1(), DomainEventCollection1), CommitOperation.Store);
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult3).CreateOrUpdate(new DomainEntity2(), DomainEventCollection1), CommitOperation.Store);
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult4).Delete(DomainEventCollection1), CommitOperation.AppendEventsOnly);
            }
        }

        [Fact]
        public async Task CommitAsyncSetsCorrectRevisionToCommitAttemptEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            var entry = subject.GetOrUpdate(EntityLoadResult1).CreateOrUpdate(new DomainEntity2(), DomainEventCollection1);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(entry.EntityLoadResult.Revision, storedCommitAttempt.Entries.First().Revision);
        }

        [Fact]
        public async Task CommitAsyncSetsCorrectConcurrencyTokenToCommitAttemptEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            var entry = subject.GetOrUpdate(EntityLoadResult1).CreateOrUpdate(new DomainEntity2(), DomainEventCollection1);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(entry.EntityLoadResult.ConcurrencyToken, storedCommitAttempt.Entries.First().ConcurrencyToken);
        }

        [Fact]
        public async Task CommitAsyncSetsCorrectDomainEventsToCommitAttemptEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            var entry = subject.GetOrUpdate(EntityLoadResult1).CreateOrUpdate(new DomainEntity2(), DomainEventCollection1);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(entry.RecordedDomainEvents, storedCommitAttempt.Entries.First().DomainEvents);
        }

        [Fact]
        public async Task CommitAsyncSetsCorrectExpectedRevisionToCommitAttemptEntryTest()
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            var entry = subject.GetOrUpdate(EntityLoadResult1).CreateOrUpdate(new DomainEntity2(), DomainEventCollection1);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Equal(entry.EntityLoadResult.TrackedLoadResult.Revision, storedCommitAttempt.Entries.First().ExpectedRevision);
        }

        [Theory]
        [ClassData(typeof(CommitAsyncSetsCorrectEntityToCommitAttemptEntryTestData))]
        public async Task CommitAsyncSetsCorrectEntityToCommitAttemptEntryTest(
            Action<UnitOfWork> entryShaper,
            object? expectedEntity)
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            entryShaper(subject);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.Same(expectedEntity, storedCommitAttempt.Entries.First().Entity);
        }

        public class CommitAsyncSetsCorrectEntityToCommitAttemptEntryTestData
           : TheoryData<Action<UnitOfWork>, object?>
        {
            public CommitAsyncSetsCorrectEntityToCommitAttemptEntryTestData()
            {
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult1).Delete(DomainEventCollection1), null);

                var de1 = new DomainEntity1();
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult2).CreateOrUpdate(de1, DomainEventCollection1), de1);

                var de2 = new DomainEntity2();
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult3).CreateOrUpdate(de2, DomainEventCollection1), de2);
                Add(unitOfWork => unitOfWork.GetOrUpdate(EntityLoadResult4).Delete(DomainEventCollection1), null);
            }
        }

        [Fact]
        public async Task CommitAsyncUnscopesEntityTest()
        {
            // Arrange
            var subject = new UnitOfWork(CreateConcurrencyTokenFactory(new ConcurrencyToken("htt")));
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var entry = subject.GetOrUpdate(EntityLoadResult1.AsScopedTo(scope)).CreateOrUpdate(new DomainEntity2(), DomainEventCollection1);

            var storageEngineMock = new Mock<IEntityStorageEngine>();
            CommitAttempt<CommitAttemptEntry> storedCommitAttempt;

            storageEngineMock.Setup(
                storageEngine => storageEngine.CommitAsync(It.IsAny<CommitAttempt<CommitAttemptEntry>>(), default))
                .Callback<CommitAttempt<CommitAttemptEntry>, CancellationToken>((commitAttempt, _) => storedCommitAttempt = commitAttempt)
                .ReturnsAsync(EntityCommitResult.Success);
            var storageEngine = storageEngineMock.Object;

            // Act
            await subject.CommitAsync(storageEngine);

            // Assert
            Assert.NotSame(entry.EntityLoadResult.GetEntity(), storedCommitAttempt.Entries.First().Entity);
        }
    }
}
