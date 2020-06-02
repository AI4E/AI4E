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
using System.Runtime.Serialization;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Indicates that an entity cannot be loaded due to a failure situation.
    /// </summary>
    [Serializable]
    public abstract class EntityLoadException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="EntityLoadException"/> class.
        /// </summary>
        protected EntityLoadException() { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityLoadException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        protected EntityLoadException(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityLoadException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The message that is the cause of the current exception.</param>
        protected EntityLoadException(string? message, Exception? innerException) : base(message, innerException) { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityLoadException"/> class.
        /// </summary>
        /// <param name="serializationInfo">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data of the exception being thrown.
        /// </param>
        /// <param name="streamingContext">
        /// The <see cref="StreamingContext"/> that contains contextual information about the source or destination.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serializationInfo"/> is <c>null</c>.
        /// </exception>
        protected EntityLoadException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        { }
    }
}