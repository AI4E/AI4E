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

namespace AI4E.Storage.Domain.Specification
{
    public abstract class EntityQueryResultScopeSpecification
    {
        protected abstract IEntityQueryResultScope CreateScope();

        [Fact]
        public void ScopeEntityNullOriginalEntityThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var scope = CreateScope();

            // Act
            void Act()
            {
                scope.ScopeEntity(null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("originalEntity", Act);
        }

        [Fact]
        public void ScopeEntityCreatesCopyTest()
        {
            // Arrange
            var scope = CreateScope();
            var originalEntity = new CopyableObject();

            // Act
            var scopedEntity = scope.ScopeEntity(originalEntity);

            // Assert
            Assert.NotSame(originalEntity, scopedEntity);
        }

        [Fact]
        public void ScopeEntityReturnsObjectOfTheSameTypeTest()
        {
            // Arrange
            var scope = CreateScope();
            var originalEntity = new CopyableObject();

            // Act
            var scopedEntity = scope.ScopeEntity(originalEntity);

            // Assert
            Assert.IsType<CopyableObject>(scopedEntity);
        }

        [Fact]
        public void ScopeEntityCreatesStructuralEqualCopyTest()
        {
            // Arrange
            var scope = CreateScope();
            var originalEntity = new CopyableObject();

            // Act
            var scopedEntity = (CopyableObject)scope.ScopeEntity(originalEntity);

            // Assert
            Assert.Equal(originalEntity.Guid, scopedEntity.Guid);
        }
    }
}
