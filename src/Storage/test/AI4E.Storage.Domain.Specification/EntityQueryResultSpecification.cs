using Xunit;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class EntityQueryResultSpecification
    {
        protected abstract IEntityQueryResult CreateQueryResult(bool loadedFromCache);

        [Fact]
        public void AsCachedResultReturnsNonNullResultTest()
        {
            // Arrange
            var subject = CreateQueryResult(false);

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
        public void AsCachedResultYieldsCorrectLoadedFromCacheTest(
            bool originalLoadedFromCache, bool loadedFromCache)
        {
            // Arrange
            var subject = CreateQueryResult(originalLoadedFromCache);

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
            var subject = CreateQueryResult(loadedFromCache);

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
        public void AsCachedResultReturnsCopyWithCorrectEntityIdentifierTest(
           bool originalLoadedFromCache, bool loadedFromCache)
        {
            // Arrange
            var subject = CreateQueryResult(originalLoadedFromCache);

            // Act
            var cachedResult = subject.AsCachedResult(loadedFromCache);

            // Assert
            Assert.Equal(subject.EntityIdentifier, cachedResult.EntityIdentifier);
        }
    }
}
