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
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class EntityLoadExceptionSpecification
    {
        protected abstract EntityLoadException Create();
        protected abstract EntityLoadException Create(string? message);
        protected abstract EntityLoadException Create(string? message, Exception? innerException);

        [Fact]
        public void ConstructionEntityIdentifierDoesNotThrowTest()
        {
            // Arrange
            // -

            // Act
            _ = Create();

            // Assert
            // -
        }

        [Fact]
        public void ConstructionWithNullMessageDoesNotThrowTest()
        {
            // Arrange
            var entityLoadException = Create(message: null);

            // Act
            var message = entityLoadException.Message;

            // Assert
            Assert.NotNull(message);
        }

        [Fact]
        public void ConstructionWithMessageIsSetCorrectlyTest()
        {
            // Arrange
            var expectedMessage = "message";
            var entityLoadException = Create(expectedMessage);

            // Act
            var message = entityLoadException.Message;

            // Assert
            Assert.Equal(expectedMessage, message);
        }

        [Fact]
        public void ConstructionWithExceptionAndMessageExceptionIsSetCorrectlyTest()
        {
            // Arrange
            var expectedException = new Exception();
            var message = "message";
            var entityLoadException = Create(message, expectedException);

            // Act
            var exception = entityLoadException.InnerException;

            // Assert
            Assert.Same(expectedException, exception);
        }

        [Fact]
        public void ConstructionWithExceptionAndMessageNullExceptionDoesNotThrowTest()
        {
            // Arrange
            var message = "message";
            var entityLoadException = Create(message, null);

            // Act
            var exception = entityLoadException.InnerException;

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void ConstructionWithExceptionAndMessageMessageIsSetCorrectlyTest()
        {
            // Arrange
            var expectedMessage = "message";
            var exception = new Exception();
            var entityLoadException = Create(expectedMessage, exception);

            // Act
            var message = entityLoadException.Message;

            // Assert
            Assert.Equal(expectedMessage, message);
        }

        [Fact]
        public void ConstructionWithExceptionAndMessageNullMessageDoesNotThrowTest()
        {
            // Arrange
            var exception = new Exception();
            var entityLoadException = Create(null, exception);

            // Act
            var message = entityLoadException.Message;

            // Assert
            Assert.NotNull(message);
        }

        [Fact]
        public void SerializationDeserializationTest()
        {
            // Arrange
            var formatter = new BinaryFormatter();
            var entityLoadException = Create();
            using var memoryStream = new MemoryStream();

            // Act
            formatter.Serialize(memoryStream, entityLoadException);
            memoryStream.Position = 0;
            var deserializedEntityLoadException = (EntityLoadException)formatter.Deserialize(memoryStream);

            // Assert
            Assert.NotNull(deserializedEntityLoadException);
        }
    }
}
