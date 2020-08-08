using System;
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class ScopeableEntityQueryResultSpecification<TQueryResult>
         where TQueryResult : class, IEntityQueryResult
    {
        protected abstract IScopeableEntityQueryResult<TQueryResult> CreateScopeableQueryResult(IEntityQueryResultScope scope);
      
        private IScopeableEntityQueryResult<TQueryResult> CreateScopeableQueryResult()
        {
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            return CreateScopeableQueryResult(scope);
        }

        #region Scoping  tests

        [Fact]
        public void AsScopedToResultReturnsNonNullResultTest()
        {
            // Arrange
            var subject = CreateScopeableQueryResult();
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.NotNull(scopedResult);
        }

        [Fact]
        public void AsScopedToYieldsCorrectScopeTest()
        {
            // Arrange
            var subject = CreateScopeableQueryResult();
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.Same(targetScope, scopedResult.Scope);
        }

        [Fact]
        public void ToQueryResultAsScopedToDoesNotCopyIfNotNecessaryTest()
        {
            // Arrange
            var scopeMock = new Mock<IEntityQueryResultScope>();
            var scope = scopeMock.Object;
            var subject = CreateScopeableQueryResult(scope).AsScopedTo(scope);

            // Act
            var scopedResult = subject.ToQueryResult();

            // Assert
            Assert.Same(subject, scopedResult);
        }

        [Fact]
        public void AsScopedToReturnsCopyWithSameEntityIdentifierTest()
        {
            // Arrange
            var subject = CreateScopeableQueryResult();
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.Equal(subject.EntityIdentifier,scopedResult.EntityIdentifier);
        }

        [Fact]
        public void AsScopedToReturnsCopyWithSameLoadedFromCacheTest()
        {
            // Arrange
            var subject = CreateScopeableQueryResult();
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.Equal(subject.LoadedFromCache, scopedResult.LoadedFromCache);
        }

        [Fact]
        public void AsScopedToReturnsCopyWithSameIsFoundTest()
        {
            // Arrange
            var subject = CreateScopeableQueryResult();
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;

            // Act
            var scopedResult = subject.AsScopedTo(targetScope);

            // Assert
            Assert.Equal(subject.IsFound(out _), scopedResult.IsFound(out _));
        }

        [Fact]
        public void ToQueryResultAsScopedToReturnsCopyWithSameEntityIdentifierTest()
        {
            // Arrange
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;
            var subject = CreateScopeableQueryResult().AsScopedTo(targetScope);

            // Act
            var scopedResult = subject.ToQueryResult();

            // Assert
            Assert.Equal(subject.EntityIdentifier, scopedResult.EntityIdentifier);
        }

        [Fact]
        public void ToQueryResultAsScopedToReturnsCopyWithSameLoadedFromCacheTest()
        {
            // Arrange
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;
            var subject = CreateScopeableQueryResult().AsScopedTo(targetScope);     

            // Act
            var scopedResult = subject.ToQueryResult();

            // Assert
            Assert.Equal(subject.LoadedFromCache, scopedResult.LoadedFromCache);
        }

        [Fact]
        public void ToQueryResultAsScopedToReturnsCopyWithSameIsFoundTest()
        {
            // Arrange
            var targetScopeMock = new Mock<IEntityQueryResultScope>();
            var targetScope = targetScopeMock.Object;
            var subject = CreateScopeableQueryResult().AsScopedTo(targetScope);

            // Act
            var scopedResult = subject.ToQueryResult();

            // Assert
            Assert.Equal(subject.IsFound(out _), scopedResult.IsFound(out _));
        }

        [Fact]
        public void AsScopedToNullScopeThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var subject = CreateScopeableQueryResult();

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
