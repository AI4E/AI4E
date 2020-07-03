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
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public abstract class EntityQueryResultTests<TQueryResult> where TQueryResult : EntityQueryResult
    {
        protected abstract TQueryResult Create(bool loadedFromCache, IEntityQueryResultScope scope);

        protected abstract IEqualityComparer<TQueryResult> GetEqualityComparer(
            bool compareScopes = true,
            bool compareLoadedFromCache = true);

        #region Tracking tests

        [Fact]
        public void IsTrackableTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);

            // Act
            var isTrackable = subject.IsTrackable<EntityQueryResult>(out var trackableEntityLoadResult);

            // Assert
            Assert.True(isTrackable);
            Assert.Same(subject, trackableEntityLoadResult);
        }

        [Fact]
        public void AsTrackedNullConcurrencyTokenFactoryThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);

            // Act
            void Act()
            {
                var trackedEntityQueryResult = subject.AsTracked(concurrencyTokenFactory: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("concurrencyTokenFactory", Act);
        }

        [Fact]
        public void AsTrackedReturnsNonNullTrackableEntityQueryResultTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);
            var concurrencyTokenFactoryMock = new Mock<IEntityConcurrencyTokenFactory>();
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
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);
            var concurrencyTokenFactoryMock = new Mock<IEntityConcurrencyTokenFactory>();
            var concurrencyTokenFactory = concurrencyTokenFactoryMock.Object;

            // Act
            var trackedEntityQueryResult = subject.AsTracked(concurrencyTokenFactory);

            // Assert
            Assert.Same(subject, trackedEntityQueryResult.TrackedLoadResult);
        }

        #endregion

        #region Caching  tests

        [Fact]
        public void AsCachedResultReturnsNonNullResultTest()
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

        [Fact]
        public void AsCachedResultReturnsObjectOfTheSameTypeTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(false, scope);

            // Act
            var cachedResult = subject.AsCachedResult(true);

            // Assert
            Assert.IsType(subject.GetType(), cachedResult);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void AsCachedResultYieldsCorrectLoadedFromCacheTest(
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
        public void AsCachedResultDoesNotCopyIfNotNecessaryTest(bool loadedFromCache)
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
        public void AsCachedResultReturnsValidCopyTest(
           bool originalLoadedFromCache, bool loadedFromCache)
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(originalLoadedFromCache, scope);

            // Act
            var cachedResult = (TQueryResult)subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Equal(
                subject,
                cachedResult,
                GetEqualityComparer(compareLoadedFromCache: false));
        }

        #endregion

        #region Scoping  tests

        [Fact]
        public void IsScopeableTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(default, scope);

            // Act
            var isScopeable = subject.IsScopeable<EntityQueryResult>(out var scopeableEntityQueryResult);

            // Assert
            Assert.True(isScopeable);
            Assert.Same(subject, scopeableEntityQueryResult);
        }

        [Fact]
        public void AsScopedToResultReturnsNonNullResultTest()
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
        public void AsScopedToResultObjectOfTheSameTypeTest()
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
            Assert.IsType(subject.GetType(), scopedResult);
        }

        [Fact]
        public void AsScopedToYieldsCorrectScopeTest()
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
        public void AsScopedToDoesNotCopyIfNotNecessaryTest()
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
        public void AsScopedToReturnsValidCopyTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = Create(false, scope);
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = (TQueryResult)subject.AsScopedTo(targetScope);

            // Assert

            Assert.Equal(
                subject,
                scopedResult,
                GetEqualityComparer(compareScopes: false));
        }

        [Fact]
        public void AsScopedToNullScopeThrowsArgumentNullExceptionTest()
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

        #endregion
    }
}
