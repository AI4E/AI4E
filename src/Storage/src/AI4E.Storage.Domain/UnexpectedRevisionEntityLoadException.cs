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
    /// An exception that indicates that the loaded entity does not match the expected revision. 
    /// </summary>
    [Serializable]
    public sealed class UnexpectedRevisionEntityLoadException : EntityLoadException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="UnexpectedRevisionEntityLoadException"/> class.
        /// </summary>
        public UnexpectedRevisionEntityLoadException()
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="UnexpectedRevisionEntityLoadException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public UnexpectedRevisionEntityLoadException(string message) : base(message)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="UnexpectedRevisionEntityLoadException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The message that is the cause of the current exception.</param>
        public UnexpectedRevisionEntityLoadException(string message, Exception innerException)
            : base(message, innerException)
        { }

        private UnexpectedRevisionEntityLoadException(
            SerializationInfo serializationInfo,
            StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        { }
    }
}