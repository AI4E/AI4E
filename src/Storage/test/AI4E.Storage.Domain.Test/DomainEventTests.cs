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
using AI4E.Storage.Domain.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public class DomainEventTests
    {
        [Fact]
        public void ConstructNullEventTypeThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("eventType", () =>
            {
                new DomainEvent(eventType: null, @event: new DomainEvent1());
            });
        }

        [Fact]
        public void ConstructNullEventThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("event", () =>
            {
                new DomainEvent(eventType: typeof(DomainEventBase), @event: null);
            });
        }

        [Fact]
        public void ConstructEventNotAssignableToEventTypeThrowsArgumentExceptionTest()
        {
            Assert.Throws<ArgumentException>("event", () =>
            {
                new DomainEvent(eventType: typeof(DomainEvent1), @event: new DomainEvent2());
            });
        }

        [Theory]
        [ClassData(typeof(ConstructIllegalEventTypeThrowsArgumentExceptionTestData))]
        public void ConstructIllegalEventTypeThrowsArgumentExceptionTest(Type eventType, object @event)
        {
            Assert.Throws<ArgumentException>("eventType", () =>
            {
                new DomainEvent(eventType, @event);
            });
        }

        public class ConstructIllegalEventTypeThrowsArgumentExceptionTestData : TheoryData<Type, object>
        {
            public ConstructIllegalEventTypeThrowsArgumentExceptionTestData()
            {
                Add(typeof(Action), new Action(() => { }));
                Add(typeof(TestEnum), default(TestEnum));
                Add(typeof(TestStruct), default(TestStruct));
                // We do not need to test for System.Void, as this is indeed a struct and the runtime *should* prevent
                // creating instances of it anyway.
                Add(typeof(IDomainEvent), new DomainEvent1());
                Add(typeof(DomainEvent<>), new DomainEvent<int>());
            }
        }

        [Theory]
        [ClassData(typeof(ConstructIllegalEventThrowsArgumentExceptionTestData))]
        public void ConstructIllegalEventThrowsArgumentExceptionTest(object @event)
        {
            Assert.Throws<ArgumentException>("event", () =>
            {
                new DomainEvent(typeof(object), @event);
            });
        }

        public class ConstructIllegalEventThrowsArgumentExceptionTestData : TheoryData<object>
        {
            public ConstructIllegalEventThrowsArgumentExceptionTestData()
            {
                Add(new Action(() => { }));
                Add(default(TestEnum));
                Add(default(TestStruct));
                // We do not need to test for System.Void, as this is indeed a struct and the runtime *should* prevent
                // creating instances of it anyway.
            }
        }

        [Fact]
        public void DefaultValueEventTypeIsTypeofObjectTest()
        {
            // Arrange
            var domainEvent = default(DomainEvent);

            // Act
            var eventType = domainEvent.EventType;

            // Assert
            Assert.Same(typeof(object), eventType);
        }

        [Fact]
        public void EventTypeIsCorrectTypeTest()
        {
            // Arrange
            var expectedEventType = typeof(DomainEventBase);
            var domainEvent = new DomainEvent(eventType: expectedEventType, @event: new DomainEvent1());

            // Act
            var eventType = domainEvent.EventType;

            // Assert
            Assert.Same(expectedEventType, eventType);
        }

        [Fact]
        public void DefaultValueEventIsOfTypeObjectTest()
        {
            // Arrange
            var domainEvent = default(DomainEvent);

            // Act
            var @event = domainEvent.Event;

            // Assert
            Assert.IsType<object>(@event);
        }

        [Fact]
        public void EventIsCorrectInstanceTest()
        {
            // Arrange
            var expectedEvent = new DomainEvent1();
            var domainEvent = new DomainEvent(eventType: typeof(DomainEventBase), @event: expectedEvent);

            // Act
            var @event = domainEvent.Event;

            // Assert
            Assert.Same(expectedEvent, @event);
        }

        [Fact]
        public void DeconstructDefaultValueEventTypeIsTypeofObjectTest()
        {
            // Arrange
            var domainEvent = default(DomainEvent);

            // Act
            var (eventType, _) = domainEvent;

            // Assert
            Assert.Same(typeof(object), eventType);
        }

        [Fact]
        public void DeconstructEventTypeIsCorrectTest()
        {
            // Arrange
            var expectedEventType = typeof(DomainEventBase);
            var domainEvent = new DomainEvent(eventType: expectedEventType, @event: new DomainEvent1());

            // Act
            var (eventType, _) = domainEvent;

            // Assert
            Assert.Same(expectedEventType, eventType);
        }

        [Fact]
        public void DeconstructDefaultValueEventIsOfTypeObjectTest()
        {
            // Arrange
            var domainEvent = default(DomainEvent);

            // Act
            var (_, @event) = domainEvent;

            // Assert
            Assert.IsType<object>(@event);
        }

        [Fact]
        public void DeconstructEventIsCorrectInstanceTest()
        {
            // Arrange
            var expectedEvent = new DomainEvent1();
            var domainEvent = new DomainEvent(eventType: typeof(DomainEventBase), @event: expectedEvent);

            // Act
            var (_, @event) = domainEvent;

            // Assert
            Assert.Same(expectedEvent, @event);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(DomainEvent left, DomainEvent right, bool expectedAreEqual)
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
        public void InequalityOperationTest(DomainEvent left, DomainEvent right, bool expectedAreEqual)
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
        public void EqualsOperationTest(DomainEvent left, DomainEvent right, bool expectedAreEqual)
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
        public void ObjectEqualsOperationTest(DomainEvent left, DomainEvent right, bool expectedAreEqual)
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
        public void EquatableEqualsOperationTest(DomainEvent left, DomainEvent right, bool expectedAreEqual)
        {
            // Arrange
            var equatable = (IEquatable<DomainEvent>)left;

            // Act
            var areEqual = equatable.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        public class EqualityTestData : TheoryData<DomainEvent, DomainEvent, bool>
        {
            public EqualityTestData()
            {
                Add(default, default, true);

                // The domain-events are not of the same instance.
                Add(new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                    new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                    false);

                var domainEvent1 = new DomainEvent1();
                Add(new DomainEvent(typeof(DomainEventBase), domainEvent1),
                    new DomainEvent(typeof(DomainEventBase), domainEvent1),
                    true);

                // Different domain-event types.
                Add(new DomainEvent(typeof(DomainEvent1), domainEvent1),
                    new DomainEvent(typeof(DomainEventBase), domainEvent1),
                    false);

                // Custom equality comparison
                Add(new DomainEvent(typeof(DomainEventBase), new ByValueEqualityDomainEvent()),
                    new DomainEvent(typeof(DomainEventBase), new ByValueEqualityDomainEvent()),
                    true);

                // Custom equality comparison & different domain-event types.
                Add(new DomainEvent(typeof(ByValueEqualityDomainEvent), new ByValueEqualityDomainEvent()),
                    new DomainEvent(typeof(DomainEventBase), new ByValueEqualityDomainEvent()),
                    false);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(DomainEvent domainEvent)
        {
            // Arrange
            var expectedHashCode = domainEvent.GetHashCode();

            // Act
            var hashCode = domainEvent.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<DomainEvent>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                Add(default);
                Add(new DomainEvent(typeof(DomainEventBase), new DomainEvent1()));
                Add(new DomainEvent(typeof(DomainEventBase), new DomainEvent2()));
                Add(new DomainEvent(typeof(DomainEventBase), new ByValueEqualityDomainEvent()));
                Add(new DomainEvent(typeof(DomainEvent1), new DomainEvent1()));
                Add(new DomainEvent(typeof(DomainEvent2), new DomainEvent2()));
                Add(new DomainEvent(typeof(ByValueEqualityDomainEvent), new ByValueEqualityDomainEvent()));
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(DomainEvent left, DomainEvent right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<DomainEvent, DomainEvent>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                Add(default, default);

                var domainEvent1 = new DomainEvent1();
                Add(new DomainEvent(typeof(DomainEventBase), domainEvent1),
                    new DomainEvent(typeof(DomainEventBase), domainEvent1));

                Add(new DomainEvent(typeof(DomainEventBase), new ByValueEqualityDomainEvent()),
                    new DomainEvent(typeof(DomainEventBase), new ByValueEqualityDomainEvent()));
            }
        }
    }
}