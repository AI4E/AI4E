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
    public sealed class NotFoundEntityQueryResultTests : EntityQueryResultTests<NotFoundEntityQueryResult>
    {
        #region C'tor tests

        [Fact]
        public void CtorNullScopeThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            static void Act()
            {
                new NotFoundEntityQueryResult(default, default, scope: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("scope", Act);
        }

        [Fact]
        public void CtorCorrectlyInitializesEntityIdentifierTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var loadedFromCache = false;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new NotFoundEntityQueryResult(entityIdentifier, loadedFromCache, scope);

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
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new NotFoundEntityQueryResult(entityIdentifier, loadedFromCache, scope);

            // Assert
            Assert.Equal(loadedFromCache, subject.LoadedFromCache);
        }

        [Fact]
        public void CtorCorrectlyInitializesScopeTest()
        {
            // Arrange
            var entityIdentifier = new EntityIdentifier(typeof(DomainEntityBase), "abc");
            var loadedFromCache = false;
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;

            // Act
            var subject = new NotFoundEntityQueryResult(entityIdentifier, loadedFromCache, scope);

            // Assert
            Assert.Same(scope, subject.Scope);
        }

        #endregion

        #region Caching  tests

        [Fact]
        public void OverlayAsCachedResultReturnsNonNullResultTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = new NotFoundEntityQueryResult(default, false, scope);

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
            var subject = new NotFoundEntityQueryResult(default, originalLoadedFromCache, scope);

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
            var subject = new NotFoundEntityQueryResult(default, loadedFromCache, scope);

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
            var subject = new NotFoundEntityQueryResult(default, originalLoadedFromCache, scope);

            // Act
            var cachedResult = subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Equal(
                subject,
                cachedResult,
                GetEqualityComparer(compareLoadedFromCache: false));
        }

        #endregion

        #region Scoping  tests

        [Fact]
        public void OverlayAsScopedToResultReturnsNonNullResultTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = new NotFoundEntityQueryResult(default, false, scope);
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
            var subject = new NotFoundEntityQueryResult(default, default, scope);
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
            var subject = new NotFoundEntityQueryResult(default, default, scope);

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
            var subject = new NotFoundEntityQueryResult(default, false, scope);
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

        [Fact]
        public void OverlayAsScopedToNullScopeThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = new NotFoundEntityQueryResult(default, false, scope);

            // Act
            void Act()
            {
                subject.AsScopedTo(scope: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("scope", Act);
        }

        #endregion

        [Fact]
        public void IsFoundTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = new NotFoundEntityQueryResult(default, default, scope);

            // Act
            var isFound = subject.IsFound(out var foundEntityQueryResult);

            // Assert
            Assert.False(isFound);
            Assert.Null(foundEntityQueryResult);
        }

        protected override NotFoundEntityQueryResult Create(bool loadedFromCache, IEntityQueryResultScope scope)
        {
            return new NotFoundEntityQueryResult(default, loadedFromCache, scope);
        }

        protected override IEqualityComparer<NotFoundEntityQueryResult> GetEqualityComparer(
            bool compareScopes = true, 
            bool compareLoadedFromCache = true)
        {
            var options = (NotFoundEntityQueryResultEquality)(-1);

            if (!compareScopes)
                options &= ~NotFoundEntityQueryResultEquality.Scope;

            if (!compareLoadedFromCache)
                options &= ~NotFoundEntityQueryResultEquality.LoadedFromCache;

            return new NotFoundEntityQueryResultEqualityComparer(options);
        }
    }
}
