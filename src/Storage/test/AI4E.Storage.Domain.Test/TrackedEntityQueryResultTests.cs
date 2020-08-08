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
using AI4E.Storage.Domain.Specification.TestTypes;
using AI4E.Storage.Domain.Test.Helpers;
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public sealed class TrackedEntityQueryResultTests : EntityQueryResultTests<TrackedEntityQueryResult>
    {
        #region Caching tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OverlayAsCachedResultReturnsNonNullResultTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, false, scope);

            // Act
            var cachedResult = subject.AsCachedResult(true);

            // Assert
            Assert.NotNull(cachedResult);
        }

        [Theory]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, false, false)]
        [InlineData(false, true, true)]
        public void OverlayAsCachedResultYieldsCorrectLoadedFromCacheTest(
            bool found, bool originalLoadedFromCache, bool loadedFromCache)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, originalLoadedFromCache, scope);

            // Act
            var cachedResult = subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Equal(loadedFromCache, cachedResult.LoadedFromCache);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public void OverlayAsCachedResultDoesNotCopyIfNotNecessaryTest(bool found, bool loadedFromCache)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, loadedFromCache, scope);

            // Act
            var cachedResult = subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Same(subject, cachedResult);
        }

        [Theory]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, false, false)]
        [InlineData(false, true, true)]
        public void OverlayAsCachedResultReturnsValidCopyTest(
           bool found, bool originalLoadedFromCache, bool loadedFromCache)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, originalLoadedFromCache, scope);

            // Act
            var cachedResult = subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Equal(
                subject,
                cachedResult,
                GetEqualityComparer(compareLoadedFromCache: false));
        }

        #endregion

        #region Scoping tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OverlayAsScopedToResultReturnsNonNullResultTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, false, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.NotNull(scopedResult);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OverlayAsScopedToYieldsCorrectScopeTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.Same(targetScope, scopedResult.Scope);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OverlayAsScopedToDoesNotCopyIfNotNecessaryTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);

            // Act
            var scopedResult = subject.AsScopedTo(scope);

            // Assert
            Assert.Same(subject, scopedResult);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OverlayAsScopedToReturnsValidCopyTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, false, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert

            Assert.Equal(
                subject,
                scopedResult,
                GetEqualityComparer(compareScopes: false));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OverlayAsScopedToNullScopeThrowsArgumentNullExceptionTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, false, scope);

            // Act
            void Act()
            {
                subject.AsScopedTo(scope: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("scope", Act);
        }

        #endregion

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyRecordedOperationsNoOperationsRecordedReturnsTrackedLoadResultTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);

            // Act
            var loadResult = subject.ApplyRecordedOperations();

            // Assert
            Assert.Same(subject.TrackedLoadResult, loadResult);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TrackedEntityLoadResultAsTrackedReturnsSameObjectTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;

            // Act
            var tracked = subject.AsTracked(concurrencyTokenFactory);

            // Assert
            Assert.Same(subject, tracked);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RecordDeleteOperationIsFoundIsFalseTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            Assert.False(deleteOperationRecorded.IsFound(out _));
        }

        [Fact]
        public void RecordDeleteOperationOnFoundQueryResultOperationSetsDefaultConcurrencyTokenTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(new ConcurrencyToken("lkpio"));
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var subject = Create(found: true, default, scope, concurrencyTokenFactory);

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            Assert.Equal(new ConcurrencyToken(), deleteOperationRecorded.ConcurrencyToken);
        }

        [Fact]
        public void RecordCreateOrUpdateOperationRecordDeleteOperationSetsNewConcurrencyTokenTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var expectedConcurrencyToken = new ConcurrencyToken("lkpio");
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(expectedConcurrencyToken);
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var subject = Create(found: true, default, scope, concurrencyTokenFactory).RecordDeleteOperation();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(new object());

            // Assert
            Assert.Equal(expectedConcurrencyToken, deleteOperationRecorded.ConcurrencyToken);
        }

        [Fact]
        public void RecordDeleteOperationOnFoundQueryResultOperationSetsDefaultRevisionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found: true, default, scope);

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            Assert.Equal(0, deleteOperationRecorded.Revision);
        }

        [Fact]
        public void RecordCreateOrUpdateOperationRecordDeleteOperationSetsNewRevisionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var subject = Create(found: true, default, scope).RecordDeleteOperation();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(new object());

            // Assert
            Assert.Equal(subject.TrackedLoadResult.Revision + 1, deleteOperationRecorded.Revision);
        }

        [Fact]
        public void RecordDeleteOperationOnFoundQueryResultOperationCorrectlyCallsConcurrencyTokenFactoryTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(new ConcurrencyToken("ggg"));
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var subject = Create(found: true, default, scope, concurrencyTokenFactory);

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            concurrencyTokenFactoryMock.Verify(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(subject.EntityIdentifier),
                Times.AtMostOnce());
        }

        [Fact]
        public void ApplyRecordRecordDeleteOperationOnFoundQueryResultOperationSetsNewRevisionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(new ConcurrencyToken("lkpio"));
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var subject = Create(found: true, default, scope, concurrencyTokenFactory).RecordDeleteOperation();

            // Act
            var deleteOperationRecorded = subject.ApplyRecordedOperations();

            // Assert
            Assert.Equal(new ConcurrencyToken(), deleteOperationRecorded.ConcurrencyToken);
        }

        [Fact]
        public void RecordDeleteOperationWithRecordedOperationsDoesNotChangeConcurrencyTokenTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var expectedConcurrencyToken = new ConcurrencyToken("lkpio");
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(new ConcurrencyToken("lkpio"));
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;

            var subject = Create(found: false, default, scope, concurrencyTokenFactory)
                .RecordCreateOrUpdateOperation(new object());

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            concurrencyTokenFactoryMock.Verify(
                 concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(subject.EntityIdentifier),
                 Times.Exactly(1));
        }

        [Fact]
        public void RecordCreateOrUpdateOperationOnNotFoundQueryResultOperationSetsNewConcurrencyTokenTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var expectedConcurrencyToken = new ConcurrencyToken("lkpio");
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(expectedConcurrencyToken);
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var subject = Create(found: false, default, scope, concurrencyTokenFactory);
            var entity = new object();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(entity);

            // Assert
            Assert.Equal(expectedConcurrencyToken, deleteOperationRecorded.ConcurrencyToken);
        }

        [Fact]
        public void RecordDeleteAfterCreateOrUpdateOperationSetsDefaultConcurrencyTokenTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var expectedConcurrencyToken = new ConcurrencyToken("lkpio");
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(expectedConcurrencyToken);
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var subject = Create(found: false, default, scope, concurrencyTokenFactory)
                .RecordCreateOrUpdateOperation(new object());

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            Assert.Equal(new ConcurrencyToken(), deleteOperationRecorded.ConcurrencyToken);
        }

        [Fact]
        public void RecordCreateOrUpdateOperationOnNotFoundQueryResultOperationSetsNewRevisionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found: false, default, scope);

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(new object());

            // Assert
            Assert.Equal(subject.TrackedLoadResult.Revision + 1, deleteOperationRecorded.Revision);
        }

        [Fact]
        public void RecordDeleteAfterCreateOrUpdateOperationSetsDefaultRevisionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found: false, default, scope)
                .RecordCreateOrUpdateOperation(new object());

            // Act
            var deleteOperationRecorded = subject.RecordDeleteOperation();

            // Assert
            Assert.Equal(0, deleteOperationRecorded.Revision);
        }

        [Fact]
        public void RecordCreateOrUpdateOperationOnNotFoundQueryResultOperationCorrectlyCallsConcurrencyTokenFactoryTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(new ConcurrencyToken("ggg"));
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var subject = Create(found: false, default, scope, concurrencyTokenFactory);
            var entity = new object();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(entity);

            // Assert
            concurrencyTokenFactoryMock.Verify(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(subject.EntityIdentifier),
                Times.Exactly(1));
        }

        [Fact]
        public void ApplyRecordRecordCreateOrUpdateOperationOnNotFoundQueryResultOperationSetsNewRevisionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var expectedConcurrencyToken = new ConcurrencyToken("lkpio");
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(expectedConcurrencyToken);
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            var entity = new object();
            var subject = Create(found: false, default, scope, concurrencyTokenFactory)
                .RecordCreateOrUpdateOperation(entity);

            // Act
            var deleteOperationRecorded = subject.ApplyRecordedOperations();

            // Assert
            Assert.Equal(expectedConcurrencyToken, deleteOperationRecorded.ConcurrencyToken);
        }

        [Fact]
        public void RecordCreateOrUpdateOperationWithRecordedOperationsDoesNotChangeConcurrencyTokenTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            var expectedConcurrencyToken = new ConcurrencyToken("lkpio");
            var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
            concurrencyTokenFactoryMock.Setup(
                concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(It.IsAny<EntityIdentifier>()))
                .Returns(new ConcurrencyToken("lkpio"));
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;

            var subject = Create(found: false, default, scope, concurrencyTokenFactory)
                .RecordCreateOrUpdateOperation(new object()).RecordDeleteOperation();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(new object());

            // Assert
            concurrencyTokenFactoryMock.Verify(
                 concurrencyTokenFactory => concurrencyTokenFactory.CreateConcurrencyToken(subject.EntityIdentifier),
                 Times.Exactly(1));
        }

        [Fact]
        public void RecordCreateOrUpdateOperationWithRecordedOperationsDoesNotChangeRevisionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found: false, default, scope)
                .RecordCreateOrUpdateOperation(new object())
                .RecordDeleteOperation();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(new object());

            // Assert
            Assert.Equal(subject.TrackedLoadResult.Revision + 1, deleteOperationRecorded.Revision);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyRecordDeleteOperationIsFoundIsFalseTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope).RecordDeleteOperation();

            // Act
            var deleteOperationRecorded = subject.ApplyRecordedOperations();

            // Assert
            Assert.False(deleteOperationRecorded.IsFound(out _));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RecordCreateOrUpdateOperationNullEntityThrowsArgumentNullExceptionTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);

            // Act
            void Act()
            {
                subject.RecordCreateOrUpdateOperation(entity: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("entity", Act);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RecordCreateOrUpdateOperationIsFoundIsTrueTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);
            var entity = new object();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(entity);

            // Assert
            Assert.True(deleteOperationRecorded.IsFound(out _));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RecordCreateOrUpdateOperationIsFoundYieldsResultWithSameEntityTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope);
            var entity = new object();

            // Act
            var deleteOperationRecorded = subject.RecordCreateOrUpdateOperation(entity);

            // Assert
            deleteOperationRecorded.IsFound(out var foundEntityQueryResult);
            Assert.Same(entity, foundEntityQueryResult.Entity);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyRecordCreateOrUpdateOperationIsFoundIsTrueTest(bool found)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var entity = new object();
            var subject = Create(found, default, scope).RecordCreateOrUpdateOperation(entity);

            // Act
            var deleteOperationRecorded = subject;

            // Assert
            Assert.True(deleteOperationRecorded.IsFound(out _));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyRecordCreateOrUpdateOperationIsFoundYieldsResultWithSameEntityTest(bool found)
        {
            // Arrange
            var entity = new object();
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(found, default, scope).RecordCreateOrUpdateOperation(entity);

            // Act
            var deleteOperationRecorded = subject.ApplyRecordedOperations();

            // Assert
            deleteOperationRecorded.IsFound(out var foundEntityQueryResult);
            Assert.Same(entity, foundEntityQueryResult.Entity);
        }

        private TrackedEntityQueryResult Create(
            bool found,
            bool loadedFromCache,
            IEntityQueryResultScope scope,
            IConcurrencyTokenFactory? concurrencyTokenFactory = null)
        {
            if (concurrencyTokenFactory is null)
            {
                var concurrencyTokenFactoryMock = new Mock<IConcurrencyTokenFactory>();
                concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;
            }

            var entityIdentifier = new EntityIdentifier(typeof(object), "abc");

            if (found)
            {
                var entity = new DomainEntity1();
                var concurrencyToken = new ConcurrencyToken("def");
                var revision = 22;

                return new FoundEntityQueryResult(
                    entityIdentifier,
                    entity,
                    concurrencyToken,
                    revision,
                    loadedFromCache,
                    scope).AsTracked(concurrencyTokenFactory);
            }
            else
            {
                return new NotFoundEntityQueryResult(
                    entityIdentifier, loadedFromCache, scope).AsTracked(concurrencyTokenFactory);
            }
        }

        protected override TrackedEntityQueryResult Create(bool loadedFromCache, IEntityQueryResultScope scope)
        {
            return Create(found: true, loadedFromCache, scope);
        }

        protected override IEqualityComparer<TrackedEntityQueryResult> GetEqualityComparer(
            bool compareScopes = true,
            bool compareLoadedFromCache = true)
        {
            var options = (TrackedEntityQueryResultEquality)(-1);

            if (!compareScopes)
            {
                options &= ~TrackedEntityQueryResultEquality.Scope;

                // If we cannot compare scopes, we may not compare entities either.
                options &= ~TrackedEntityQueryResultEquality.Entity;
            }

            if (!compareLoadedFromCache)
                options &= ~TrackedEntityQueryResultEquality.LoadedFromCache;

            return new TrackedEntityQueryResultEqualityComparer(options);
        }
    }
}
