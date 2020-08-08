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
using System.ComponentModel;
using AI4E.Storage.Domain.Specification.TestTypes;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public class DomainEventTypeConverterTests
    {
        [Fact]
        public void ResolvesCorrectTypeConverterTest()
        {
            // Arrange
            // -

            // Act
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Assert
            Assert.IsType<DomainEventTypeConverter>(typeConverter);
        }

        [Fact]
        public void CanConvertFromIdentityConversionTest()
        {
            // Arrange
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var canConvertFrom = typeConverter.CanConvertFrom(typeof(DomainEvent));

            // Assert
            Assert.True(canConvertFrom);
        }

        [Fact]
        public void CanConvertFromTypeObjectValueTupleTest()
        {
            // Arrange
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var canConvertFrom = typeConverter.CanConvertFrom(typeof((Type, object)));

            // Assert
            Assert.True(canConvertFrom);
        }

        [Fact]
        public void CanConvertFromObjectFailsTest()
        {
            // Arrange
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var canConvertFrom = typeConverter.CanConvertFrom(typeof(object));

            // Assert
            Assert.False(canConvertFrom);
        }

        [Fact]
        public void CanConvertToIdentityConversionTest()
        {
            // Arrange
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var canConvertTo = typeConverter.CanConvertTo(typeof(DomainEvent));

            // Assert
            Assert.True(canConvertTo);
        }

        [Fact]
        public void CanConvertToTypeObjectValueTupleTest()
        {
            // Arrange
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var canConvertTo = typeConverter.CanConvertTo(typeof((Type, object)));

            // Assert
            Assert.True(canConvertTo);
        }

        [Fact]
        public void CanConvertToObjectFailsTest()
        {
            // Arrange
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var canConvertTo = typeConverter.CanConvertTo(typeof(object));

            // Assert
            Assert.False(canConvertTo);
        }

        [Fact]
        public void ConvertFromIdentityConversionTest()
        {
            // Arrange
            var expectedDomainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var domainEvent = typeConverter.ConvertFrom(expectedDomainEvent);

            // Assert
            Assert.Equal(expectedDomainEvent, domainEvent);
        }

        [Fact]
        public void ConvertFromTypeObjectValueTupleTest()
        {
            // Arrange
            var expectedDomainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var tuple = (expectedDomainEvent.EventType, expectedDomainEvent.Event);
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var domainEvent = typeConverter.ConvertFrom(tuple);

            // Assert
            Assert.Equal(expectedDomainEvent, domainEvent);
        }

        [Fact]
        public void ConvertFromObjectThrowsNotSupportedExceptionTest()
        {
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            Assert.Throws<NotSupportedException>(() =>
            {
                typeConverter.ConvertFrom(new object());
            });
        }

        [Fact]
        public void ConvertToIdentityConversionTest()
        {
            // Arrange
            var expectedDomainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var domainEvent = typeConverter.ConvertTo(expectedDomainEvent, typeof(DomainEvent));

            // Assert
            Assert.Equal(expectedDomainEvent, domainEvent);
        }

        [Fact]
        public void ConvertToTypeObjectValueTupleTest()
        {
            // Arrange
            var domainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var expectedTuple = (domainEvent.EventType, domainEvent.Event);
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            // Act
            var tuple = typeConverter.ConvertTo(domainEvent, typeof((Type, object)));

            // Assert
            Assert.Equal(expectedTuple, tuple);
        }

        [Fact]
        public void ConvertToObjectThrowsNotSupportedExceptionTest()
        {
            var domainEvent = new DomainEvent(typeof(DomainEventBase), new DomainEvent1());
            var typeConverter = TypeDescriptor.GetConverter(typeof(DomainEvent));

            Assert.Throws<NotSupportedException>(() =>
            {
                typeConverter.ConvertTo(domainEvent, typeof(object));
            });
        }
    }
}
