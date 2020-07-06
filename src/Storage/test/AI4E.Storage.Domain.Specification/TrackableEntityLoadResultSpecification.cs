using System;
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class TrackableEntityLoadResultSpecification<TLoadResult>
        where TLoadResult : class, IEntityLoadResult
    {
        protected abstract ITrackableEntityLoadResult<TLoadResult> CreateTrackableLoadResult();

        private ITrackedEntityLoadResult<TLoadResult> CreateTrackedLoadResult(
            IConcurrencyTokenFactory? concurrencyTokenFactory = null)
        {
            if (concurrencyTokenFactory is null)
            {
                var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
                concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            }

            var trackableLoadResult = CreateTrackableLoadResult();
            return trackableLoadResult.AsTracked(concurrencyTokenFactory);
        }

        [Fact]
        public void IsTrackableTest()
        {
            // Arrange
            var subject = CreateTrackableLoadResult();

            // Act
            var isTrackable = subject.IsTrackable<TLoadResult>(out var trackableEntityLoadResult);

            // Assert
            Assert.True(isTrackable);
            Assert.Same(subject, trackableEntityLoadResult);
        }

        [Fact]
        public void AsTrackedNullConcurrencyTokenFactoryThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var subject = CreateTrackableLoadResult();

            // Act
            void Act()
            {
                var trackedEntityQueryResult = subject.AsTracked(concurrencyTokenFactory: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("concurrencyTokenFactory", Act);
        }

        [Fact]
        public void AsTrackedReturnsNonNullTrackedEntityQueryResultTest()
        {
            // Arrange
            var subject = CreateTrackableLoadResult();
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;

            // Act
            var trackedEntityQueryResult = subject.AsTracked(concurrencyTokenFactory);

            // Assert
            Assert.NotNull(trackedEntityQueryResult);
        }

        [Fact]
        public void AsTrackedCorrectlyInitializesTrackedLoadResultTest()
        {
            // Arrange
            var subject = CreateTrackableLoadResult();
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;

            // Act
            var trackedEntityQueryResult = subject.AsTracked(concurrencyTokenFactory);

            // Assert
            Assert.Same(subject, trackedEntityQueryResult.TrackedLoadResult);
        }

        [Fact]
        public void ApplyRecordedOperationsNoOperationsRecordedReturnsTrackedLoadResultTest()
        {
            // Arrange
            var subject = CreateTrackedLoadResult();

            // Act
            var loadResult = subject.ApplyRecordedOperations();

            // Assert
            Assert.Same(subject.TrackedLoadResult, loadResult);
        }

        [Fact]
        public void TrackedEntityLoadResultAsTrackedReturnsSameObjectTest()
        {
            // Arrange
            var subject = CreateTrackedLoadResult();
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;

            // Act
            var tracked = subject.AsTracked(concurrencyTokenFactory);

            // Assert
            Assert.Same(subject, tracked);
        }

        [Fact]
        public void RecordDeleteOperationIsFoundIsFalseTest()
        {
            // Arrange
            var subject = CreateTrackedLoadResult();

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            Assert.False(deleteOperationRecorded.IsFound(out _));
        }

        [Fact]
        public void ApplyRecordDeleteOperationIsFoundIsFalseTest()
        {
            // Arrange
            var subject = CreateTrackedLoadResult().RecordDeleteOperation();

            // Act
            var deleteOperationRecorded = subject.ApplyRecordedOperations();

            // Assert
            Assert.False(deleteOperationRecorded.IsFound(out _));
        }

        [Fact]
        public void RecordCreateOrUpdateOperationNullEntityThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var subject = CreateTrackedLoadResult();

            // Act
            void Act()
            {
                subject.RecordCreateOrUpdateOperation(entity: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("entity", Act);
        }

        [Fact]
        public void RecordCreateOrUpdateOperationIsFoundIsTrueTest()
        {
            // Arrange
            var subject = CreateTrackedLoadResult();
            var entity = new object();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(entity);

            // Assert
            Assert.True(deleteOperationRecorded.IsFound(out _));
        }

        [Fact]
        public void RecordCreateOrUpdateOperationIsFoundYieldsResultWithSameEntityTest()
        {
            // Arrange
            var subject = CreateTrackedLoadResult();
            var entity = new object();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(entity);

            // Assert
            deleteOperationRecorded.IsFound(out var foundEntityQueryResult);
            Assert.Same(entity, foundEntityQueryResult.Entity);
        }

        [Fact]
        public void ApplyRecordCreateOrUpdateOperationIsFoundIsTrueTest()
        {
            // Arrange
            var entity = new object();
            var subject = CreateTrackedLoadResult()
                .RecordCreateOrUpdateOperation(entity);

            // Act
            var deleteOperationRecorded = subject;

            // Assert
            Assert.True(deleteOperationRecorded.IsFound(out _));
        }

        [Fact]
        public void ApplyRecordCreateOrUpdateOperationIsFoundYieldsResultWithSameEntityTest()
        {
            // Arrange
            var entity = new object();
            var subject = CreateTrackedLoadResult()
                .RecordCreateOrUpdateOperation(entity);

            // Act
            var deleteOperationRecorded = subject.ApplyRecordedOperations();

            // Assert
            deleteOperationRecorded.IsFound(out var foundEntityQueryResult);
            Assert.Same(entity, foundEntityQueryResult.Entity);
        }
    }
}
