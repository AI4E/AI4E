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
    public class EntityDescriptorTests
    {
        [Fact]
        public void ConstructNullEntityTypeThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entityType", () =>
            {
                new EntityDescriptor(entityType: null, entity: new DomainEntity1());
            });
        }

        [Fact]
        public void ConstructNullEntityThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entity", () =>
            {
                new EntityDescriptor(entityType: typeof(DomainEntityBase), entity: null);
            });
        }

        [Fact]
        public void ConstructEntityNotAssignableToEntityTypeThrowsArgumentExceptionTest()
        {
            Assert.Throws<ArgumentException>("entity", () =>
            {
                new EntityDescriptor(entityType: typeof(DomainEntity1), entity: new DomainEntity2());
            });
        }

        [Theory]
        [ClassData(typeof(ConstructIllegalEntityTypeThrowsArgumentExceptionTestData))]
        public void ConstructIllegalEntityTypeThrowsArgumentExceptionTest(Type entityType, object entity)
        {
            Assert.Throws<ArgumentException>("entityType", () =>
            {
                new EntityDescriptor(entityType, entity);
            });
        }

        public class ConstructIllegalEntityTypeThrowsArgumentExceptionTestData : TheoryData<Type, object>
        {
            public ConstructIllegalEntityTypeThrowsArgumentExceptionTestData()
            {
                Add(typeof(Action), new Action(() => { }));
                Add(typeof(TestEnum), default(TestEnum));
                Add(typeof(TestStruct), default(TestStruct));
                // We do not need to test for System.Void, as this is indeed a struct and the runtime *should* prevent
                // creating instances of it anyway.
                Add(typeof(IDomainEntity), new DomainEntity1());
                Add(typeof(DomainEntity<>), new DomainEntity<int>());
            }
        }

        [Theory]
        [ClassData(typeof(ConstructIllegalEntityThrowsArgumentExceptionTestData))]
        public void ConstructIllegalEntityThrowsArgumentExceptionTest(object entity)
        {
            Assert.Throws<ArgumentException>("entity", () =>
            {
                new EntityDescriptor(typeof(object), entity);
            });
        }

        public class ConstructIllegalEntityThrowsArgumentExceptionTestData : TheoryData<object>
        {
            public ConstructIllegalEntityThrowsArgumentExceptionTestData()
            {
                Add(new Action(() => { }));
                Add(default(TestEnum));
                Add(default(TestStruct));
                // We do not need to test for System.Void, as this is indeed a struct and the runtime *should* prevent
                // creating instances of it anyway.
            }
        }

        [Fact]
        public void ConstructFromEntityNullEntityThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entity", () =>
            {
                new EntityDescriptor(entity: null);
            });
        }

        [Theory]
        [ClassData(typeof(ConstructIllegalEntityThrowsArgumentExceptionTestData))]
        public void ConstructFromEntityIllegalEntityThrowsArgumentExceptionTest(object entity)
        {
            Assert.Throws<ArgumentException>("entity", () =>
            {
                new EntityDescriptor(entity);
            });
        }

        [Fact]
        public void DefaultValueEntityTypeIsTypeofObjectTest()
        {
            // Arrange
            var entityDescriptor = default(EntityDescriptor);

            // Act
            var entityType = entityDescriptor.EntityType;

            // Assert
            Assert.Same(typeof(object), entityType);
        }

        [Fact]
        public void EntityTypeIsCorrectTypeTest()
        {
            // Arrange
            var expectedEntityType = typeof(DomainEntityBase);
            var entityDescriptor = new EntityDescriptor(entityType: expectedEntityType, entity: new DomainEntity1());

            // Act
            var entityType = entityDescriptor.EntityType;

            // Assert
            Assert.Same(expectedEntityType, entityType);
        }

        [Fact]
        public void DefaultValueEntityIsOfTypeObjectTest()
        {
            // Arrange
            var entityDescriptor = default(EntityDescriptor);

            // Act
            var entity = entityDescriptor.Entity;

            // Assert
            Assert.IsType<object>(entity);
        }

        [Fact]
        public void EntityIsCorrectInstanceTest()
        {
            // Arrange
            var expectedEntity = new DomainEntity1();
            var entityDescriptor = new EntityDescriptor(entityType: typeof(DomainEntityBase), entity: expectedEntity);

            // Act
            var entity = entityDescriptor.Entity;

            // Assert
            Assert.Same(expectedEntity, entity);
        }

        [Fact]
        public void EntityIsCorrectInstanceWhenCreatedFromEntityTest()
        {
            // Arrange
            var expectedEntity = new DomainEntity1();
            var entityDescriptor = new EntityDescriptor(entity: expectedEntity);

            // Act
            var entity = entityDescriptor.Entity;

            // Assert
            Assert.Same(expectedEntity, entity);
        }

        [Fact]
        public void EntityTypeIsCorrectTypeWhenCreatedFromEntityTest()
        {
            // Arrange
            var entity = new DomainEntity1();
            var exectedEntityType = entity.GetType();
            var entityDescriptor = new EntityDescriptor(entity);

            // Act
            var entityType = entityDescriptor.EntityType;

            // Assert
            Assert.Same(exectedEntityType, entityType);
        }

        [Fact]
        public void DeconstructDefaultValueEntityTypeIsTypeofObjectTest()
        {
            // Arrange
            var entityDescriptor = default(EntityDescriptor);

            // Act
            var (entityType, _) = entityDescriptor;

            // Assert
            Assert.Same(typeof(object), entityType);
        }

        [Fact]
        public void DeconstructEntityTypeIsCorrectTest()
        {
            // Arrange
            var expectedEntityType = typeof(DomainEntityBase);
            var entityDescriptor = new EntityDescriptor(entityType: expectedEntityType, entity: new DomainEntity1());

            // Act
            var (entityType, _) = entityDescriptor;

            // Assert
            Assert.Same(expectedEntityType, entityType);
        }

        [Fact]
        public void DeconstructDefaultValueEntityIsOfTypeObjectTest()
        {
            // Arrange
            var entityDescriptor = default(EntityDescriptor);

            // Act
            var (_, entity) = entityDescriptor;

            // Assert
            Assert.IsType<object>(entity);
        }

        [Fact]
        public void DeconstructEntityIsCorrectInstanceTest()
        {
            // Arrange
            var expectedEntity = new DomainEntity1();
            var entityDescriptor = new EntityDescriptor(entityType: typeof(DomainEntityBase), entity: expectedEntity);

            // Act
            var (_, entity) = entityDescriptor;

            // Assert
            Assert.Same(expectedEntity, entity);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(EntityDescriptor left, EntityDescriptor right, bool expectedAreEqual)
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
        public void InequalityOperationTest(EntityDescriptor left, EntityDescriptor right, bool expectedAreEqual)
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
        public void EqualsOperationTest(EntityDescriptor left, EntityDescriptor right, bool expectedAreEqual)
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
        public void ObjectEqualsOperationTest(EntityDescriptor left, EntityDescriptor right, bool expectedAreEqual)
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
        public void EquatableEqualsOperationTest(EntityDescriptor left, EntityDescriptor right, bool expectedAreEqual)
        {
            // Arrange
            var equatable = (IEquatable<EntityDescriptor>)left;

            // Act
            var areEqual = equatable.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        [Fact]
        public void ObjectEqualsOperationDoesNotEqualOtherTypeTest()
        {
            // Arrange
            var left = new EntityDescriptor();
            var other = new object();

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void ObjectEqualsOperationDoesNotEqualNullTest()
        {
            // Arrange
            var left = new EntityDescriptor();
            object other = null;

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.False(areEqual);
        }

        public class EqualityTestData : TheoryData<EntityDescriptor, EntityDescriptor, bool>
        {
            public EqualityTestData()
            {
                Add(default, default, true);

                // The entities are not of the same instance.
                Add(new EntityDescriptor(typeof(DomainEntityBase), new DomainEntity1()),
                    new EntityDescriptor(typeof(DomainEntityBase), new DomainEntity1()),
                    false);

                var DomainEntity1 = new DomainEntity1();
                Add(new EntityDescriptor(typeof(DomainEntityBase), DomainEntity1),
                    new EntityDescriptor(typeof(DomainEntityBase), DomainEntity1),
                    true);

                // Different domain-entity types.
                Add(new EntityDescriptor(typeof(DomainEntity1), DomainEntity1),
                    new EntityDescriptor(typeof(DomainEntityBase), DomainEntity1),
                    false);

                // Custom equality comparison
                Add(new EntityDescriptor(typeof(DomainEntityBase), new ByValueEqualityDomainEntity()),
                    new EntityDescriptor(typeof(DomainEntityBase), new ByValueEqualityDomainEntity()),
                    true);

                // Custom equality comparison & different domain-entity types.
                Add(new EntityDescriptor(typeof(ByValueEqualityDomainEntity), new ByValueEqualityDomainEntity()),
                    new EntityDescriptor(typeof(DomainEntityBase), new ByValueEqualityDomainEntity()),
                    false);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(EntityDescriptor entityDescriptor)
        {
            // Arrange
            var expectedHashCode = entityDescriptor.GetHashCode();

            // Act
            var hashCode = entityDescriptor.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<EntityDescriptor>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                Add(default);
                Add(new EntityDescriptor(typeof(DomainEntityBase), new DomainEntity1()));
                Add(new EntityDescriptor(typeof(DomainEntityBase), new DomainEntity2()));
                Add(new EntityDescriptor(typeof(DomainEntityBase), new ByValueEqualityDomainEntity()));
                Add(new EntityDescriptor(typeof(DomainEntity1), new DomainEntity1()));
                Add(new EntityDescriptor(typeof(DomainEntity2), new DomainEntity2()));
                Add(new EntityDescriptor(typeof(ByValueEqualityDomainEntity), new ByValueEqualityDomainEntity()));
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(EntityDescriptor left, EntityDescriptor right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<EntityDescriptor, EntityDescriptor>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                Add(default, default);

                var DomainEntity1 = new DomainEntity1();
                Add(new EntityDescriptor(typeof(DomainEntityBase), DomainEntity1),
                    new EntityDescriptor(typeof(DomainEntityBase), DomainEntity1));

                Add(new EntityDescriptor(typeof(DomainEntityBase), new ByValueEqualityDomainEntity()),
                    new EntityDescriptor(typeof(DomainEntityBase), new ByValueEqualityDomainEntity()));
            }
        }
    }
}