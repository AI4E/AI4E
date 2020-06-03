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
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public class EntityLoadResultExtensionTests
    {
        [Fact]
        public void GetEntityDescriptorNullEntityLoadResultThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entityLoadResult", () =>
            {
                EntityLoadResultExtension.GetEntityDescriptor(null);
            });
        }

        [Fact]
        public void GetEntityDescriptorTest()
        {
            // Arrange

            var entityType = typeof(DomainEntityBase);
            var entity = new DomainEntity1();
            var entityIdentifier = new EntityIdentifier(entityType, "abc");
            var expectedEntityDescriptor = new EntityDescriptor(entityType, entity);
            var entityLoadResultMock = new Mock<ISuccessEntityLoadResult>();
            entityLoadResultMock.Setup(entityLoadResult => entityLoadResult.EntityIdentifier).Returns(entityIdentifier);
            entityLoadResultMock.Setup(entityLoadResult => entityLoadResult.Entity).Returns(entity);

            // Act
            var entityDescriptor = EntityLoadResultExtension.GetEntityDescriptor(entityLoadResultMock.Object);

            // Assert
            Assert.Equal(expectedEntityDescriptor, entityDescriptor);
        }

        [Fact]
        public void IsSuccessNullEntityLoadResultThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entityLoadResult", () =>
            {
                EntityLoadResultExtension.IsSuccess(null);
            });
        }

        [Fact]
        public void IsSuccessWithEntityResultNullEntityLoadResultThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("entityLoadResult", () =>
            {
                EntityLoadResultExtension.IsSuccess(null, out object entity);
            });
        }

        [Theory]
        [ClassData(typeof(IsSuccessTestData))]
        public void IsSuccessTest(object? entity, bool expectedIsSuccess)
        {
            // Arrange
            var entityLoadResultMock = new Mock<IEntityLoadResult>();
            entityLoadResultMock.Setup(entityLoadResult => entityLoadResult.GetEntity(It.IsAny<bool>()))
                .Returns(entity);

            // Act
            var isSuccess = EntityLoadResultExtension.IsSuccess(entityLoadResultMock.Object);

            // Assert
            Assert.Equal(expectedIsSuccess, isSuccess);
        }

        [Theory]
        [ClassData(typeof(IsSuccessTestData))]
        public void IsSuccessWithEntityResultTest(object? entity, bool expectedIsSuccess)
        {
            // Arrange
            var entityLoadResultMock = new Mock<IEntityLoadResult>();
            entityLoadResultMock.Setup(entityLoadResult => entityLoadResult.GetEntity(It.IsAny<bool>()))
                .Returns(entity);

            // Act
            var isSuccess = EntityLoadResultExtension.IsSuccess(entityLoadResultMock.Object, out _);

            // Assert
            Assert.Equal(expectedIsSuccess, isSuccess);
        }

        public class IsSuccessTestData : TheoryData<object?, bool>
        {
            public IsSuccessTestData()
            {
                Add(null, false);
                Add(new DomainEntity1(), true);
                Add(new DomainEntity2(), true);
            }
        } 

        [Theory]
        [ClassData(typeof(IsSuccessWithEntityResultYieldCorrectEntityTestat))]
        public void IsSuccessWithEntityResultYieldCorrectEntityTest(object? expectedEntity)
        {
            // Arrange
            var entityLoadResultMock = new Mock<IEntityLoadResult>();
            entityLoadResultMock.Setup(entityLoadResult => entityLoadResult.GetEntity(It.IsAny<bool>()))
                .Returns(expectedEntity);

            // Act
            EntityLoadResultExtension.IsSuccess(entityLoadResultMock.Object, out var entity);

            // Assert
            Assert.Same(expectedEntity, entity);
        }

        public class IsSuccessWithEntityResultYieldCorrectEntityTestat : TheoryData<object?>
        {
            public IsSuccessWithEntityResultYieldCorrectEntityTestat()
            {
                Add(null);
                Add(new DomainEntity1());
                Add(new DomainEntity2());
            }
        }

    }
}
