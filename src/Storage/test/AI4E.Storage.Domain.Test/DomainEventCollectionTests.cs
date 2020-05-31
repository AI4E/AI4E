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
using System.Collections.Immutable;
using System.Linq;
using AI4E.Storage.Domain.Test.Helpers;
using AI4E.Storage.Domain.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public class DomainEventCollectionTests
    {
        private static readonly DomainEventCollection _collection0 = new DomainEventCollection(new DomainEvent[] { });

        private static readonly DomainEventCollection _collection1 = new DomainEventCollection(new[]
        {
            new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
            new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
            new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
        });

        private static readonly DomainEventCollection _collection2 = new DomainEventCollection(new[]
        {
            new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
            new DomainEvent(typeof(DomainEventBase), new DomainEvent2())
        });

        private static readonly DomainEventCollection _collection3 = new DomainEventCollection(new[]
        {
            new DomainEvent(typeof(DomainEventBase), new DomainEvent1())
        });

        private static readonly DomainEventCollection _collection4 = new DomainEventCollection(new DomainEvent[]
        {
            new DomainEvent()
        });

        private static readonly DomainEventCollection _collection5 = new DomainEventCollection(new DomainEvent());

        [Fact]
        public void ConstructFromEnumerableNullDomainEventsThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("domainEvents", () =>
            {
                new DomainEventCollection((IEnumerable<DomainEvent>)null);
            });
        }

        [Fact]
        public void ConstructFromImmutableHashSetNullDomainEventsThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("domainEvents", () =>
            {
                new DomainEventCollection((ImmutableHashSet<DomainEvent>)null);
            });
        }

        [Fact]
        public void DefaultCountIsZeroTest()
        {
            // Arrange
            var domainEventCollection = default(DomainEventCollection);

            // Act
            var count = domainEventCollection.Count;

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void DefaultIterationTest()
        {
            // Arrange
            var domainEventCollection = default(DomainEventCollection);
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Empty(domainEvents);
        }

        [Fact]
        public void ConstructFromSingleDomainEventCountIsOneTest()
        {
            // Arrange
            var domainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var domainEventCollection = new DomainEventCollection(domainEvent);

            // Act
            var count = domainEventCollection.Count;

            // Assert
            Assert.Equal(1, count);
        }

        [Fact]
        public void ConstructFromSingleDomainEventIterationTest()
        {
            // Arrange
            var expectedDomainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var domainEventCollection = new DomainEventCollection(expectedDomainEvent);
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Single(domainEvents, expectedDomainEvent);
        }

        [Fact]
        public void ConstructFromEnumerableEliminatesDuplicatesTest()
        {
            // Arrange
            var expectedDomainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var domainEventCollection = new DomainEventCollection(new[] { expectedDomainEvent, expectedDomainEvent });
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Single(domainEvents, expectedDomainEvent);
        }

        [Fact]
        public void ConstructFromEnumerableIterationTest()
        {
            // Arrange
            var expectedDomainEvents = new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
            }.ToImmutableHashSet();

            var domainEventCollection = new DomainEventCollection(expectedDomainEvents);
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Equal(expectedDomainEvents, domainEvents);
        }

        [Fact]
        public void ConstructFromImmutableHashSetIterationTest()
        {
            // Arrange
            var expectedDomainEvents = new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
            }.ToImmutableHashSet();

            var domainEventCollection = new DomainEventCollection(expectedDomainEvents);
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Equal(expectedDomainEvents, domainEvents);
        }

        [Fact]
        public void ConstructFromImmutableHashSetWithNonDefaultComparerIterationTest()
        {
            var domainEvent2 = new DomainEvent2();

            // Arrange
            var immutableHashSet = new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
                new DomainEvent(typeof(DomainEventBase), domainEvent2),
                new DomainEvent(typeof(DomainEventBase), domainEvent2),
            }.ToImmutableHashSet(new ConstantEqualityComparer<DomainEvent>(false));

            var expectedDomainEvent = immutableHashSet.ToImmutableHashSet(EqualityComparer<DomainEvent>.Default);
            var domainEventCollection = new DomainEventCollection(immutableHashSet);
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Equal(expectedDomainEvent, domainEvents);
        }

        [Fact]
        public void ConstructFromEnumerableWithImmutableHashSetIterationTest()
        {
            // Arrange
            var expectedDomainEvents = new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
            }.ToImmutableHashSet();

            var domainEventCollection = new DomainEventCollection((IEnumerable<DomainEvent>)expectedDomainEvents);
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Equal(expectedDomainEvents, domainEvents);
        }

        [Fact]
        public void ConstructFromEnumerableWithImmutableHashSetWithNonDefaultComparerIterationTest()
        {
            var domainEvent2 = new DomainEvent2();

            // Arrange
            var immutableHashSet = new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
                new DomainEvent(typeof(DomainEventBase), domainEvent2),
                new DomainEvent(typeof(DomainEventBase), domainEvent2),
            }.ToImmutableHashSet(new ConstantEqualityComparer<DomainEvent>(false));

            var expectedDomainEvent = immutableHashSet.ToImmutableHashSet(EqualityComparer<DomainEvent>.Default);
            var domainEventCollection = new DomainEventCollection((IEnumerable<DomainEvent>)immutableHashSet);
            var domainEvents = new List<DomainEvent>();

            // Act
            foreach (var domainEvent in domainEventCollection)
            {
                domainEvents.Add(domainEvent);
            }

            // Assert
            Assert.Equal(expectedDomainEvent, domainEvents);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(
            DomainEventCollection left, DomainEventCollection right, bool expectedAreEqual)
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
        public void InequalityOperationTest(
            DomainEventCollection left, DomainEventCollection right, bool expectedAreEqual)
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
        public void EqualsOperationTest(DomainEventCollection left, DomainEventCollection right, bool expectedAreEqual)
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
        public void ObjectEqualsOperationTest(
            DomainEventCollection left, DomainEventCollection right, bool expectedAreEqual)
        {
            // Arrange
            var other = (object)right;

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        public class EqualityTestData : TheoryData<DomainEventCollection, DomainEventCollection, bool>
        {
            public EqualityTestData()
            {
                Add(default, default, true);
                Add(default, _collection0, true);
                Add(default, _collection1, false);
                Add(default, _collection2, false);
                Add(default, _collection3, false);
                Add(default, _collection4, false);
                Add(default, _collection5, false);
                Add(_collection0, _collection0, true);
                Add(_collection0, _collection1, false);
                Add(_collection0, _collection2, false);
                Add(_collection0, _collection3, false);
                Add(_collection0, _collection4, false);
                Add(_collection0, _collection5, false);
                Add(_collection1, _collection0, false);
                Add(_collection1, _collection1, true);
                Add(_collection1, _collection2, false);
                Add(_collection1, _collection3, false);
                Add(_collection1, _collection4, false);
                Add(_collection1, _collection5, false);
                Add(_collection2, _collection0, false);
                Add(_collection2, _collection1, false);
                Add(_collection2, _collection2, true);
                Add(_collection2, _collection3, false);
                Add(_collection2, _collection4, false);
                Add(_collection2, _collection5, false);
                Add(_collection3, _collection0, false);
                Add(_collection3, _collection1, false);
                Add(_collection3, _collection2, false);
                Add(_collection3, _collection3, true);
                Add(_collection3, _collection4, false);
                Add(_collection3, _collection5, false);
                Add(_collection4, _collection0, false);
                Add(_collection4, _collection1, false);
                Add(_collection4, _collection2, false);
                Add(_collection4, _collection3, false);
                Add(_collection4, _collection4, true);
                Add(_collection4, _collection5, true);
                Add(_collection5, _collection0, false);
                Add(_collection5, _collection1, false);
                Add(_collection5, _collection2, false);
                Add(_collection5, _collection3, false);
                Add(_collection5, _collection4, true);
                Add(_collection5, _collection5, true);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(DomainEventCollection domainEvent)
        {
            // Arrange
            var expectedHashCode = domainEvent.GetHashCode();

            // Act
            var hashCode = domainEvent.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<DomainEventCollection>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                Add(default);
                Add(_collection0);
                Add(_collection1);
                Add(_collection2);
                Add(_collection3);
                Add(_collection4);
                Add(_collection5);
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(DomainEventCollection left, DomainEventCollection right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<DomainEventCollection, DomainEventCollection>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                Add(default, default);
                Add(default, _collection0);
                Add(_collection0, _collection0);
                Add(_collection1, _collection1);
                Add(_collection2, _collection2);
                Add(_collection3, _collection3);
                Add(_collection4, _collection4);
                Add(_collection5, _collection5);
                Add(_collection4, _collection5);
            }
        }

        [Fact]
        public void DefaultGetEnumeratorTest()
        {
            // Arrange
            var domainEventCollection = default(DomainEventCollection);

            // Act
            var result = domainEventCollection.Any();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetEnumeratorTest()
        {
            // Arrange
            var expectedDomainEvents = new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent2()),
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
            }.ToImmutableHashSet();

            var domainEventCollection = new DomainEventCollection(expectedDomainEvents);

            // Act
            var hashSet = domainEventCollection.ToImmutableHashSet();

            // Assert
            Assert.Equal(expectedDomainEvents, hashSet);
        }

        [Fact]
        public void ConcatWithDefaultReturnsCurrentTest()
        {
            // Arrange
            var current = _collection1;
            var other = default(DomainEventCollection);

            // Act
            var result = current.Concat(other);

            // Assert
            Assert.Equal(current, result);
        }

        [Fact]
        public void ConcatDefaultWithOtherReturnsOtherTest()
        {
            // Arrange
            var current = default(DomainEventCollection);
            var other = _collection1;

            // Act
            var result = current.Concat(other);

            // Assert
            Assert.Equal(other, result);
        }

        [Fact]
        public void ConcatEliminatesDuplicatesTest()
        {
            // Arrange       
            var domainEvent1 = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var domainEvent2 = new DomainEvent(typeof(DomainEventBase), new DomainEvent2());
            var domainEvent3 = default(DomainEvent);
            var current = new DomainEventCollection(new[] { domainEvent1, domainEvent2 });
            var other = new DomainEventCollection(new[] { domainEvent2, domainEvent3 });
            var expected = new DomainEventCollection(new[] { domainEvent1, domainEvent2, domainEvent3 });

            // Act
            var result = current.Concat(other);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
