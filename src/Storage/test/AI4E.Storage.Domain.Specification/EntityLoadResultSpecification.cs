using Xunit;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class EntityLoadResultSpecification
    {
        protected abstract IEntityLoadResult CreateLoadResult();

        [Fact]
        public void IsFoundYieldsNonNullResultIfFoundTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isFound = loadResult.IsFound(out var result);

            // Assert
            if (isFound)
            {
                Assert.NotNull(result);
            }
        }

        [Fact]
        public void IsFoundYieldsResultWithEqualIdentifierIfFoundTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isFound = loadResult.IsFound(out var result);

            // Assert
            if (isFound)
            {
                Assert.Equal(loadResult.EntityIdentifier, result.EntityIdentifier);
            }
        }

        [Fact]
        public void IsVerificationFailedYieldsNonNullResultIfVerificationFailedTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isVerificationFailed = loadResult.IsVerificationFailed(out var result);

            // Assert
            if (isVerificationFailed)
            {
                Assert.NotNull(result);
            }
        }

        [Fact]
        public void IsVerificationFailedYieldResultWithEqualIdentifierIfVerificationFailedTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isVerificationFailed = loadResult.IsVerificationFailed(out var result);

            // Assert
            if (isVerificationFailed)
            {
                Assert.Equal(loadResult.EntityIdentifier, result.EntityIdentifier);
            }
        }

        [Fact]
        public void IsScopeableYieldsNonNullResultIfScopeableTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isScopeable = loadResult.IsScopeable<IEntityQueryResult>(out var result);

            // Assert
            if (isScopeable)
            {
                Assert.NotNull(result);
            }
        }

        [Fact]
        public void IsScopeableYieldsResultWithEqualIdentifierIfScopeableTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isScopeable = loadResult.IsScopeable<IEntityQueryResult>(out var result);

            // Assert
            if (isScopeable)
            {
                Assert.Equal(loadResult.EntityIdentifier, result.EntityIdentifier);
            }
        }

        [Fact]
        public void IsTrackableYieldsNonNullResultIfTrackableTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isTrackable = loadResult.IsTrackable<IEntityLoadResult>(out var result);

            // Assert
            if (isTrackable)
            {
                Assert.NotNull(result);
            }
        }

        [Fact]
        public void IsTrackableYieldsResultWithEqualIdentifierIfTrackableTest()
        {
            // Arrange
            var loadResult = CreateLoadResult();

            // Act
            var isTrackable = loadResult.IsTrackable<IEntityLoadResult>(out var result);

            // Assert
            if (isTrackable)
            {
                Assert.Equal(loadResult.EntityIdentifier, result.EntityIdentifier);
            }
        }
    }
}
