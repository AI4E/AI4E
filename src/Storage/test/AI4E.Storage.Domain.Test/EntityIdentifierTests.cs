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
using AI4E.Storage.Domain.Specification.TestTypes;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public class EntityIdentifierTests
    {
        [Fact]
        public void ConstructNullEntityTypeThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entityType", () =>
            {
                new EntityIdentifier(entityType: null, entityId: "abc");
            });
        }

        [Fact]
        public void ConstructNullEntityThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entityId", () =>
            {
                new EntityIdentifier(entityType: typeof(DomainEntityBase), entityId: null);
            });
        }

        [Theory]
        [ClassData(typeof(ConstructIllegalEntityTypeThrowsArgumentExceptionTestData))]
        public void ConstructIllegalEntityTypeThrowsArgumentExceptionTest(Type entityType)
        {
            Assert.Throws<ArgumentException>("entityType", () =>
            {
                new EntityIdentifier(entityType, entityId: "abc");
            });
        }

        public class ConstructIllegalEntityTypeThrowsArgumentExceptionTestData : TheoryData<Type>
        {
            public ConstructIllegalEntityTypeThrowsArgumentExceptionTestData()
            {
                Add(typeof(Action));
                Add(typeof(TestEnum));
                Add(typeof(TestStruct));
                // We do not need to test for System.Void, as this is indeed a struct and the runtime *should* prevent
                // creating instances of it anyway.
                Add(typeof(IDomainEntity));
                Add(typeof(DomainEntity<>));
            }
        }

        [Fact]
        public void DefaultValueEntityTypeIsTypeofObjectTest()
        {
            // Arrange
            var entityIdentifier = default(EntityIdentifier);

            // Act
            var entityType = entityIdentifier.EntityType;

            // Assert
            Assert.Same(typeof(object), entityType);
        }

        [Fact]
        public void EntityTypeIsCorrectTypeTest()
        {
            // Arrange
            var expectedEntityType = typeof(DomainEntityBase);
            var entityIdentifier = new EntityIdentifier(entityType: expectedEntityType, entityId: "abc");

            // Act
            var entityType = entityIdentifier.EntityType;

            // Assert
            Assert.Same(expectedEntityType, entityType);
        }

        [Fact]
        public void DefaultValueEntityIdIsEmptyStringTest()
        {
            // Arrange
            var entityIdentifier = default(EntityIdentifier);

            // Act
            var entityId = entityIdentifier.EntityId;

            // Assert
            Assert.Equal(string.Empty, entityId);
        }

        [Fact]
        public void EntityIdIsCorrectTest()
        {
            // Arrange
            var expectedEntityId = "abc";
            var entityIdentifier = new EntityIdentifier(entityType: typeof(DomainEntityBase), entityId: expectedEntityId);

            // Act
            var entityId = entityIdentifier.EntityId;

            // Assert
            Assert.Equal(expectedEntityId, entityId);
        }

        [Fact]
        public void DeconstructDefaultValueEntityTypeIsTypeofObjectTest()
        {
            // Arrange
            var entityIdentifier = default(EntityIdentifier);

            // Act
            var (entityType, _) = entityIdentifier;

            // Assert
            Assert.Same(typeof(object), entityType);
        }

        [Fact]
        public void DeconstructEntityTypeIsCorrectTest()
        {
            // Arrange
            var expectedEntityType = typeof(DomainEntityBase);
            var entityIdentifier = new EntityIdentifier(entityType: expectedEntityType, entityId: "abc");

            // Act
            var (entityType, _) = entityIdentifier;

            // Assert
            Assert.Same(expectedEntityType, entityType);
        }

        [Fact]
        public void DeconstructDefaultValueEntityIdIsEmtptyStringTest()
        {
            // Arrange
            var entityIdentifier = default(EntityIdentifier);

            // Act
            var (_, entityId) = entityIdentifier;

            // Assert
            Assert.Equal(string.Empty, entityId);
        }

        [Fact]
        public void DeconstructEntityIsIsCorrectTest()
        {
            // Arrange
            var expectedEntityId = "abc";
            var entityIdentifier = new EntityIdentifier(entityType: typeof(DomainEntityBase), entityId: expectedEntityId);

            // Act
            var (_, entityId) = entityIdentifier;

            // Assert
            Assert.Equal(expectedEntityId, entityId);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(EntityIdentifier left, EntityIdentifier right, bool expectedAreEqual)
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
        public void InequalityOperationTest(EntityIdentifier left, EntityIdentifier right, bool expectedAreEqual)
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
        public void EqualsOperationTest(EntityIdentifier left, EntityIdentifier right, bool expectedAreEqual)
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
        public void ObjectEqualsOperationTest(EntityIdentifier left, EntityIdentifier right, bool expectedAreEqual)
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
        public void EquatableEqualsOperationTest(EntityIdentifier left, EntityIdentifier right, bool expectedAreEqual)
        {
            // Arrange
            var equatable = (IEquatable<EntityIdentifier>)left;

            // Act
            var areEqual = equatable.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        public class EqualityTestData : TheoryData<EntityIdentifier, EntityIdentifier, bool>
        {
            public EqualityTestData()
            {
                Add(default, default, true);
                Add(new EntityIdentifier(typeof(object), string.Empty), default, true);
                Add(default, new EntityIdentifier(typeof(object), string.Empty), true);

                Add(new EntityIdentifier(typeof(DomainEntityBase), "abc"),
                    new EntityIdentifier(typeof(DomainEntityBase), "def"),
                    false);

                Add(new EntityIdentifier(typeof(DomainEntityBase), "abc"),
                   new EntityIdentifier(typeof(DomainEntityBase), "abc"),
                   true);

                // Different domain-entity types.
                Add(new EntityIdentifier(typeof(DomainEntity1), "abc"),
                    new EntityIdentifier(typeof(DomainEntityBase), "abc"),
                    false);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(EntityIdentifier entityIdentifier)
        {
            // Arrange
            var expectedHashCode = entityIdentifier.GetHashCode();

            // Act
            var hashCode = entityIdentifier.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<EntityIdentifier>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                Add(default);
                Add(new EntityIdentifier(typeof(DomainEntityBase), ""));
                Add(new EntityIdentifier(typeof(DomainEntityBase), "abc"));
                Add(new EntityIdentifier(typeof(DomainEntity1), "def"));
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(EntityIdentifier left, EntityIdentifier right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<EntityIdentifier, EntityIdentifier>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                Add(default, default);
                Add(new EntityIdentifier(typeof(object), string.Empty), default);
                Add(new EntityIdentifier(typeof(DomainEntityBase), "abc"),
                    new EntityIdentifier(typeof(DomainEntityBase), "abc"));
                Add(new EntityIdentifier(typeof(DomainEntity1), "def"),
                    new EntityIdentifier(typeof(DomainEntity1), "def"));

            }
        }
    }
}
