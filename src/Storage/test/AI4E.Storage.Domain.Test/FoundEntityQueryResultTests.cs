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
using AI4E.Utils;
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public sealed class FoundEntityQueryResultTests : EntityQueryResultTests<FoundEntityQueryResult>
    {
        // TODO: This is a 90% Copy of NotFoundEntityQueryResultTests. Use some code generation mechanism...

        #region C'tor tests

        [Fact]
        public void CtorNullEntityThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");        
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 22;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            void Act()
            {
                new FoundEntityQueryResult(entityIdentifier, entity: null, concurrencyToken, revision, default, scope);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("entity", Act);
        }

        [Fact]
        public void CtorEntityNotAssignableToEntityTypeThrowsArgumentExceptionTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntity2), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 22;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            void Act()
            {
                new FoundEntityQueryResult(entityIdentifier, entity, concurrencyToken, revision, default, scope);
            }

            // Assert
            Assert.Throws<ArgumentException>("entity", Act);
        }

        [Fact]
        public void CtorEntityIsDelegateThrowsArgumentExceptionTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(object), "abc");
            var entity = new Action(() => { });
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 22;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            void Act()
            {
                new FoundEntityQueryResult(entityIdentifier, entity, concurrencyToken, revision, default, scope);
            }

            // Assert
            Assert.Throws<ArgumentException>("entity", Act);
        }

        [Fact]
        public void CtorEntityIsValueTypeThrowsArgumentExceptionTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(object), "abc");
            var entity = new TestStruct();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 22;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            void Act()
            {
                new FoundEntityQueryResult(entityIdentifier, entity, concurrencyToken, revision, default, scope);
            }

            // Assert
            Assert.Throws<ArgumentException>("entity", Act);
        }

        [Fact]
        public void CtorNegativeRevisionThrowsArgumentOutOfRangeExceptionTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = -5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            void Act()
            {
                new FoundEntityQueryResult(entityIdentifier, entity, concurrencyToken, revision, default, scope);
            }

            // Assert
            Assert.Throws<ArgumentOutOfRangeException>("revision", Act);
        }

        [Fact]
        public void CtorNullScopeThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 22;

            // Act
            void Act()
            {
                new FoundEntityQueryResult(entityIdentifier, entity, concurrencyToken, revision, default, scope: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("scope", Act);
        }

        [Fact]
        public void CtorCorrectlyInitializesEntityIdentifierTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new FoundEntityQueryResult(
                entityIdentifier, entity, concurrencyToken, revision, default, scope);

            // Assert
            Assert.Equal(entityIdentifier, subject.EntityIdentifier);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CtorCorrectlyInitializesLoadedFromCacheTest(bool loadedFromCache)
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new FoundEntityQueryResult(
               entityIdentifier, entity, concurrencyToken, revision, loadedFromCache, scope);

            // Assert
            Assert.Equal(loadedFromCache, subject.LoadedFromCache);
        }

        [Fact]
        public void CtorCorrectlyInitializesScopeTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new FoundEntityQueryResult(
              entityIdentifier, entity, concurrencyToken, revision, default, scope);

            // Assert
            Assert.Same(scope, subject.Scope);
        }

        [Fact]
        public void CtorCorrectlyInitializesConcurrencyTokenTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new FoundEntityQueryResult(
              entityIdentifier, entity, concurrencyToken, revision, default, scope);

            // Assert
            Assert.Equal(concurrencyToken, subject.ConcurrencyToken);
        }

        [Fact]
        public void CtorCorrectlyInitializesRevisionTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new FoundEntityQueryResult(
              entityIdentifier, entity, concurrencyToken, revision, default, scope);

            // Assert
            Assert.Equal(revision, subject.Revision);
        }

        [Fact]
        public void CtorCorrectlyInitializesEntityTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new FoundEntityQueryResult(
              entityIdentifier, entity, concurrencyToken, revision, default, scope);

            // Assert
            Assert.Same(entity, subject.Entity);
        }

        #endregion

        #region Caching tests

        [Fact]
        public void OverlayAsCachedResultReturnsNonNullResultTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(false, scope);

            // Act
            var cachedResult = subject.AsCachedResult(true);

            // Assert
            Assert.NotNull(cachedResult);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void OverlayAsCachedResultYieldsCorrectLoadedFromCacheTest(
            bool originalLoadedFromCache, bool loadedFromCache)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(originalLoadedFromCache, scope);

            // Act
            var cachedResult = subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Equal(loadedFromCache, cachedResult.LoadedFromCache);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OverlayAsCachedResultDoesNotCopyIfNotNecessaryTest(bool loadedFromCache)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(loadedFromCache, scope);

            // Act
            var cachedResult = subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Same(subject, cachedResult);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void OverlayAsCachedResultReturnsValidCopyTest(
           bool originalLoadedFromCache, bool loadedFromCache)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(originalLoadedFromCache, scope);

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

        [Fact]
        public void OverlayAsScopedToResultReturnsNonNullResultTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(false, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.NotNull(scopedResult);
        }

        [Fact]
        public void OverlayAsScopedToYieldsCorrectScopeTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.Same(targetScope, scopedResult.Scope);
        }

        [Fact]
        public void OverlayAsScopedToDoesNotCopyIfNotNecessaryTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);

            // Act
            var scopedResult = subject.AsScopedTo(scope);

            // Assert
            Assert.Same(subject, scopedResult);
        }

        [Fact]
        public void OverlayAsScopedToReturnsValidCopyTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(false, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            targetScopeMock
                .Setup(targetScope => targetScope.ScopeEntity(It.IsAny<object>()))
                .Returns<object>(originalEntity => originalEntity.DeepClone());
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert

            Assert.Equal(
                subject,
                scopedResult,
                GetEqualityComparer(compareScopes: false));
        }

        [Fact]
        public void OverlayAsScopedToNullScopeThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(false, scope);

            // Act
            void Act()
            {
                subject.AsScopedTo(scope: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("scope", Act);
        }

        [Fact]
        public void AsScopedToCreatesEntityCopyTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(object), "abc");
            var entity = new CopyableObject();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = new FoundEntityQueryResult(
              entityIdentifier, entity, concurrencyToken, revision, default, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.NotSame(entity, scopedResult.Entity);
        }

        [Fact]
        public void AsScopedToCreatesEntityCopyOfTheSameTypeTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(object), "abc");
            var entity = new CopyableObject();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = new FoundEntityQueryResult(
              entityIdentifier, entity, concurrencyToken, revision, default, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            targetScopeMock
                .Setup(targetScope => targetScope.ScopeEntity(It.IsAny<object>()))
                .Returns<object>(originalEntity => originalEntity.DeepClone());
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.IsType(entity.GetType(), scopedResult.Entity);
        }

        [Fact]
        public void AsScopedToCreatesStructuralEqualEntityCopyTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(object), "abc");
            var entity = new CopyableObject();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 5;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = new FoundEntityQueryResult(
              entityIdentifier, entity, concurrencyToken, revision, default, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            targetScopeMock
                .Setup(targetScope => targetScope.ScopeEntity(It.IsAny<object>()))
                .Returns<object>(originalEntity => originalEntity.DeepClone());
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.Equal(entity.Guid, ((CopyableObject)scopedResult.Entity).Guid);
        }

        #endregion

        [Fact]
        public void IsFoundTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);

            // Act
            var isFound = subject.IsFound(out var foundEntityQueryResult);

            // Assert
            Assert.True(isFound);
            Assert.Same(subject, foundEntityQueryResult);
        }

        protected override FoundEntityQueryResult Create(bool loadedFromCache, IEntityQueryResultScope scope)
        {
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var entity = new DomainEntity1();
            var concurrencyToken = new ConcurrencyToken("def");
            var revision = 22;

            return new FoundEntityQueryResult(
                entityIdentifier, entity, concurrencyToken, revision, loadedFromCache, scope);
        }

        protected override IEqualityComparer<FoundEntityQueryResult> GetEqualityComparer(
            bool compareScopes = true,
            bool compareLoadedFromCache = true)
        {
            var options = (FoundEntityQueryResultEquality)(-1);

            if (!compareScopes)
            {
                options &= ~FoundEntityQueryResultEquality.Scope;

                // If we cannot compare scopes, we may not compare entities either.
                options &= ~FoundEntityQueryResultEquality.Entity;
            }

            if (!compareLoadedFromCache)
                options &= ~FoundEntityQueryResultEquality.LoadedFromCache;

            return new FoundEntityQueryResultEqualityComparer(options);
        }
    }
}
