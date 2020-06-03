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

using AI4E.Storage.Domain.Specification.TestTypes;
using Xunit;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class CommitAttemptEntrySpecification
    {
        protected abstract ICommitAttemptEntry Create(
            CommitOperation commitOperation = default,
            EntityIdentifier entityIdentifier = default,
            long revision = default,
            ConcurrencyToken concurrencyToken = default,
            DomainEventCollection domainEvents = default,
            long expectedRevision = default,
            object? entity = default);

        [Theory]
        [ClassData(typeof(CommitOperationTestData))]
        public void CommitOperationTest(CommitOperation expectedCommitOperation)
        {
            // Arrange
            var commitAttemptEntry = Create(commitOperation: expectedCommitOperation);

            // Act
            var commitOperation = commitAttemptEntry.Operation;

            // Assert
            Assert.Equal(expectedCommitOperation, commitOperation);
        }

        public class CommitOperationTestData : TheoryData<CommitOperation>
        {
            public CommitOperationTestData()
            {
                Add(CommitOperation.Store);
                Add(CommitOperation.Delete);
            }
        }

        [Fact]
        public void EntityIdentifierTest()
        {
            // Arrange
            var expectedEntityIdentifier = new EntityIdentifier(typeof(DomainEntity1), "abc");
            var commitAttemptEntry = Create(entityIdentifier: expectedEntityIdentifier);

            // Act
            var entityIdentifier = commitAttemptEntry.EntityIdentifier;

            // Assert
            Assert.Equal(expectedEntityIdentifier, entityIdentifier);
        }

        [Fact]
        public void RevisionTest()
        {
            // Arrange       
            var expectedRevision = 22;
            var commitAttemptEntry = Create(revision: expectedRevision);

            // Act
            var revision = commitAttemptEntry.Revision;

            // Assert
            Assert.Equal(expectedRevision, revision);
        }

        [Fact]
        public void ConcurrencyTokenTest()
        {
            // Arrange       
            var expectedConcurrencyToken = new ConcurrencyToken("abc");
            var commitAttemptEntry = Create(concurrencyToken: expectedConcurrencyToken);

            // Act
            var concurrencyToken = commitAttemptEntry.ConcurrencyToken;

            // Assert
            Assert.Equal(expectedConcurrencyToken, concurrencyToken);
        }

        [Fact]
        public void DomainEventsTest()
        {
            // Arrange       
            var expectedDomainEvents = new DomainEventCollection(new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEvent2), new DomainEvent2())
            });

            var commitAttemptEntry = Create(domainEvents: expectedDomainEvents);

            // Act
            var domainEvents = commitAttemptEntry.DomainEvents;

            // Assert
            Assert.Equal(expectedDomainEvents, domainEvents);
        }

        [Fact]
        public void ExpectedRevisionTest()
        {
            // Arrange
            var expectedExpectedRevision = 22;
            var commitAttemptEntry = Create(expectedRevision: expectedExpectedRevision);

            // Act
            var expectedRevision = commitAttemptEntry.ExpectedRevision;

            // Assert
            Assert.Equal(expectedExpectedRevision, expectedRevision);
        }

        [Theory]
        [ClassData(typeof(EntityTestData))]
        public void EntityTest(object? expectedEntity)
        {
            // Arrange       
            var commitAttemptEntry = Create(entity: expectedEntity);

            // Act
            var entity = commitAttemptEntry.Entity;

            // Assert
            Assert.Same(expectedEntity, entity);
        }

        public class EntityTestData : TheoryData<object?>
        {
            public EntityTestData()
            {
                Add(new DomainEntity1());
                Add(new DomainEntity2());
                Add(null);
            }
        }
    }
}
