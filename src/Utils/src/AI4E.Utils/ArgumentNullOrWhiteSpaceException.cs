/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

using System.Runtime.Serialization;

namespace System
{
    /// <summary>
    /// Thrown if an argument is null or a string that is empty or consists of whitespace only.
    /// </summary>
    [Serializable]
    public class ArgumentNullOrWhiteSpaceException : ArgumentException
    {
        /// <summary>
        /// Creates a new instance of type <see cref="ArgumentNullOrWhiteSpaceException"/>.
        /// </summary>
        public ArgumentNullOrWhiteSpaceException() : base("The argument must neither be null nor an empty string or a string that consists of whitespace only.") { }

        /// <summary>
        /// Creates a new instance of type <see cref="ArgumentNullOrWhiteSpaceException"/>.
        /// </summary>
        /// <param name="paramName">The name of the parameter that caused the exception.</param>
        public ArgumentNullOrWhiteSpaceException(string paramName) : base("The argument must neither be null nor an empty string or a string that consists of whitespace only.", paramName) { }

        /// <summary>
        /// Creates a new instance of type <see cref="ArgumentNullOrWhiteSpaceException"/>.
        /// </summary>
        /// <param name="message">The error message that explains the reason for this exception.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception,
        /// or <c>null</c> if no inner exception is specified.
        /// </param>
        public ArgumentNullOrWhiteSpaceException(string message, Exception innerException) : base(message, "The argument must neither be null nor an empty string or a string that consists of whitespace only.", innerException) { }

        /// <summary>
        /// Creates a new instance of type <see cref="ArgumentNullOrWhiteSpaceException"/>.
        /// </summary>
        /// <param name="message">The error message that explains the reason for this exception.</param>
        /// <param name="paramName">The name of the parameter that caused the exception.</param>
        public ArgumentNullOrWhiteSpaceException(string paramName, string message) : base(message, paramName) { }

        /// <summary>
        /// Creates a new instance of type <see cref="ArgumentNullOrWhiteSpaceException"/> with serialized data.
        /// </summary>
        /// <param name="info">The object that holds the serialized object data.</param>
        /// <param name="context">n object that describes the source or destination of the serialized data.</param>
        protected ArgumentNullOrWhiteSpaceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
