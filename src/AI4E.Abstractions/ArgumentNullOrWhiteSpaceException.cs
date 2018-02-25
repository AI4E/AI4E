/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        ArgumentNullOrWhiteSpaceException.cs 
 * Types:           AI4E.ArgumentNullOrWhiteSpaceException
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   25.02.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

namespace AI4E
{
    [Serializable]
    public class ArgumentNullOrWhiteSpaceException : ArgumentException
    {
        public ArgumentNullOrWhiteSpaceException() : base("The argument must neither be null nor an empty string or a string that consists of whitespace only.") { }

        public ArgumentNullOrWhiteSpaceException(string paramName) : base("The argument must neither be null nor an empty string or a string that consists of whitespace only.", paramName) { }

        public ArgumentNullOrWhiteSpaceException(string message, Exception innerException) : base(message, "The argument must neither be null nor an empty string or a string that consists of whitespace only.", innerException) { }

        public ArgumentNullOrWhiteSpaceException(string paramName, string message) : base(message, paramName) { }

        protected ArgumentNullOrWhiteSpaceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
