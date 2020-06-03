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
using AI4E.Storage.Domain.Specification;
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public class CommitAttemptEntryTests : CommitAttemptEntrySpecification
    {
        protected override ICommitAttemptEntry Create(
            CommitOperation commitOperation,
            EntityIdentifier entityIdentifier,
            long revision,
            ConcurrencyToken concurrencyToken,
            DomainEventCollection domainEvents,
            long expectedRevision,
            object? entity)
        {
            var trackedEntityMock = new Mock<ITrackedEntity>();
            var entityLoadResultMock = new Mock<ICacheableEntityLoadResult>();

            // Setup entity-identifier
            entityLoadResultMock.Setup(entityLoadResult => entityLoadResult.EntityIdentifier).Returns(entityIdentifier);

            // Setup expected-revision
            entityLoadResultMock.Setup(entityLoadResult => entityLoadResult.Revision)
               .Returns(expectedRevision);

            trackedEntityMock.Setup(trackedEntity => trackedEntity.OriginalEntityLoadResult)
                .Returns(entityLoadResultMock.Object);

            // Setup track-state, that will be converted to commit operation
            trackedEntityMock.Setup(trackedEntity => trackedEntity.TrackState)
                .Returns(ReverseConvert(commitOperation, expectedRevision));

            // Setup revision
            trackedEntityMock.Setup(trackedEntity => trackedEntity.Revision).Returns(revision);

            // Setup concurrency-token 
            trackedEntityMock.Setup(trackedEntity => trackedEntity.ConcurrencyToken).Returns(concurrencyToken);

            // Setup entity
            trackedEntityMock.Setup(trackedEntity => trackedEntity.Entity).Returns(entity);

            // Setup domain-events
            trackedEntityMock.Setup(trackedEntity => trackedEntity.DomainEvents).Returns(domainEvents);

            return new CommitAttemptEntry(trackedEntityMock.Object);
        }

        private EntityTrackState ReverseConvert(CommitOperation commitOperation, long expectedRevision)
        {
            if (commitOperation == CommitOperation.Delete)
                return EntityTrackState.Deleted;

            if (expectedRevision == 0)
                return EntityTrackState.Created;

            return EntityTrackState.Updated;
        }

        [Fact]
        public void ConstructNullTrackedEntityThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("trackedEntity", () =>
            {
                new CommitAttemptEntry(trackedEntity: null);
            });
        }

        [Theory]
        [ClassData(typeof(ConstructIllegalEntityTrackStateThrowsArgumentExceptionTestData))]
        public void ConstructIllegalEntityTrackStateThrowsArgumentExceptionTest(EntityTrackState entityTrackState)
        {
            var mock = new Mock<ITrackedEntity>();
            mock.Setup(trackedEntity => trackedEntity.TrackState).Returns(entityTrackState);

            Assert.Throws<ArgumentException>("trackedEntity", () =>
            {
                new CommitAttemptEntry(trackedEntity: mock.Object);
            });
        }

        public class ConstructIllegalEntityTrackStateThrowsArgumentExceptionTestData : TheoryData<EntityTrackState>
        {
            public ConstructIllegalEntityTrackStateThrowsArgumentExceptionTestData()
            {
                Add(EntityTrackState.NonExistent);
                Add(EntityTrackState.Unchanged);
                Add(EntityTrackState.Untracked);
                Add((EntityTrackState)52);
            }
        }

        [Theory]
        [ClassData(typeof(NativeCommitOperationTestData))]
        public void NativeCommitOperationTest(EntityTrackState entityTrackState, CommitOperation expectedCommitOperation)
        {
            // Arrange
            var trackedEntityMock = new Mock<ITrackedEntity>();
            trackedEntityMock.Setup(trackedEntity => trackedEntity.TrackState).Returns(entityTrackState);
            var commitAttemptEntry = new CommitAttemptEntry(trackedEntityMock.Object);

            // Act
            var commitOperation = commitAttemptEntry.Operation;

            // Assert
            Assert.Equal(expectedCommitOperation, commitOperation);
        }

        public class NativeCommitOperationTestData : TheoryData<EntityTrackState, CommitOperation>
        {
            public NativeCommitOperationTestData()
            {
                Add(EntityTrackState.Created, CommitOperation.Store);
                Add(EntityTrackState.Updated, CommitOperation.Store);
                Add(EntityTrackState.Deleted, CommitOperation.Delete);
            }
        }

        // The default value shall describe deleting a non-existing entry.
        [Fact]
        public void DefaultValueCommitOperationIsDeleteTest()
        {
            // Arrange
            var commitAttemptEntry = default(CommitAttemptEntry);

            // Act
            var commitOperation = commitAttemptEntry.Operation;

            // Assert
            Assert.Equal(CommitOperation.Delete, commitOperation);
        }

        [Fact]
        public void DefaultValueEntityIdentifierTest()
        {
            // Arrange
            var expectedEntityIdentifier = default(EntityIdentifier);
            var commitAttemptEntry = default(CommitAttemptEntry);

            // Act
            var entityIdentifier = commitAttemptEntry.EntityIdentifier;

            // Assert
            Assert.Equal(expectedEntityIdentifier, entityIdentifier);
        }

        [Fact]
        public void DefaultValueRevisionTest()
        {
            // Arrange       
            var expectedRevision = 0L;
            var commitAttemptEntry = default(CommitAttemptEntry);

            // Act
            var revision = commitAttemptEntry.Revision;

            // Assert
            Assert.Equal(expectedRevision, revision);
        }

        [Fact]
        public void DefaultValueConcurrencyTokenTest()
        {
            // Arrange   
            var expectedConcurrencyToken = default(ConcurrencyToken);
            var commitAttemptEntry = default(CommitAttemptEntry);

            // Act
            var concurrencyToken = commitAttemptEntry.ConcurrencyToken;

            // Assert
            Assert.Equal(expectedConcurrencyToken, concurrencyToken);
        }

        [Fact]
        public void DefaultValueDomainEventsTest()
        {
            // Arrange   
            var expectedDomainEvents = default(DomainEventCollection);
            var commitAttemptEntry = default(CommitAttemptEntry);

            // Act
            var domainEvents = commitAttemptEntry.DomainEvents;

            // Assert
            Assert.Equal(expectedDomainEvents, domainEvents);
        }

        [Fact]
        public void DefaultValueExpectedRevisionTest()
        {
            // Arrange
            var expectedExpectedRevision = 0L;
            var commitAttemptEntry = default(CommitAttemptEntry);

            // Act
            var expectedRevision = commitAttemptEntry.ExpectedRevision;

            // Assert
            Assert.Equal(expectedExpectedRevision, expectedRevision);
        }

        [Fact]
        public void DefaultValueEntityTest()
        {
            // Arrange    
            var commitAttemptEntry = default(CommitAttemptEntry);

            // Act
            var entity = commitAttemptEntry.Entity;

            // Assert
            Assert.Null(entity);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(CommitAttemptEntry left, CommitAttemptEntry right, bool expectedAreEqual)
        {
            // Arrange
            // -

            // Act
            var areEqual = left == right;

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void InequalityOperationTest(CommitAttemptEntry left, CommitAttemptEntry right, bool expectedAreEqual)
        {
            // Arrange
            // -

            // Act
            var areNotEqual = left != right;

            // Assert
            Assert.Equal(!expectedAreEqual, areNotEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualsOperationTest(CommitAttemptEntry left, CommitAttemptEntry right, bool expectedAreEqual)
        {
            // Arrange
            // -

            // Act
            var areEqual = left.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void ObjectEqualsOperationTest(CommitAttemptEntry left, CommitAttemptEntry right, bool expectedAreEqual)
        {
            // Arrange
            var other = (object)right;

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EquatableEqualsOperationTest(
            CommitAttemptEntry left, CommitAttemptEntry right, bool expectedAreEqual)
        {
            // Arrange
            var equatable = (IEquatable<CommitAttemptEntry>)left;

            // Act
            var areEqual = equatable.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        public class EqualityTestData : TheoryData<CommitAttemptEntry, CommitAttemptEntry, bool>
        {
            public EqualityTestData()
            {
                Add(default, default, true);

                var mock1 = new Mock<ITrackedEntity>();
                mock1.Setup(trackedEntity => trackedEntity.TrackState).Returns(EntityTrackState.Created);
                var commitAttemptyEntry1 = new CommitAttemptEntry(mock1.Object);

                var mock2 = new Mock<ITrackedEntity>();
                mock2.Setup(trackedEntity => trackedEntity.TrackState).Returns(EntityTrackState.Updated);
                var commitAttemptyEntry2 = new CommitAttemptEntry(mock2.Object);

                Add(default, commitAttemptyEntry1, false);
                Add(default, commitAttemptyEntry2, false);

                Add(commitAttemptyEntry1, commitAttemptyEntry1, true);
                Add(commitAttemptyEntry1, commitAttemptyEntry2, false);

                Add(commitAttemptyEntry2, commitAttemptyEntry1, false);
                Add(commitAttemptyEntry2, commitAttemptyEntry2, true);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(CommitAttemptEntry entityIdentifier)
        {
            // Arrange
            var expectedHashCode = entityIdentifier.GetHashCode();

            // Act
            var hashCode = entityIdentifier.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<CommitAttemptEntry>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                var mock1 = new Mock<ITrackedEntity>();
                mock1.Setup(trackedEntity => trackedEntity.TrackState).Returns(EntityTrackState.Created);
                var commitAttemptyEntry1 = new CommitAttemptEntry(mock1.Object);

                var mock2 = new Mock<ITrackedEntity>();
                mock2.Setup(trackedEntity => trackedEntity.TrackState).Returns(EntityTrackState.Updated);
                var commitAttemptyEntry2 = new CommitAttemptEntry(mock2.Object);

                Add(default);
                Add(commitAttemptyEntry1);
                Add(commitAttemptyEntry2);
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(CommitAttemptEntry left, CommitAttemptEntry right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<CommitAttemptEntry, CommitAttemptEntry>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                Add(default, default);

                var mock1 = new Mock<ITrackedEntity>();
                mock1.Setup(trackedEntity => trackedEntity.TrackState).Returns(EntityTrackState.Created);
                var commitAttemptyEntry1 = new CommitAttemptEntry(mock1.Object);

                var mock2 = new Mock<ITrackedEntity>();
                mock2.Setup(trackedEntity => trackedEntity.TrackState).Returns(EntityTrackState.Updated);
                var commitAttemptyEntry2 = new CommitAttemptEntry(mock2.Object);

                Add(commitAttemptyEntry1, commitAttemptyEntry1);
                Add(commitAttemptyEntry2, commitAttemptyEntry2);
            }
        }
    }
}
