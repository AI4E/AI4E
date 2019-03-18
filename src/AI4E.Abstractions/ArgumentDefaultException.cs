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

using System;
using System.Runtime.Serialization;

namespace AI4E
{
    /// <summary>
    /// Thrown if an argument is the default value of its type or null if the type is a value type.
    /// </summary>
    public class ArgumentDefaultException : ArgumentException
    {
        private const string _defaultMessage = "A non-default value must be specified.";

        public ArgumentDefaultException() : base(_defaultMessage) { }

        public ArgumentDefaultException(string paramName) : base(_defaultMessage, paramName) { }

        public ArgumentDefaultException(string message, Exception innerException) : base(message, innerException) { }

        public ArgumentDefaultException(string message, string paramName) : base(message, paramName) { }

        public ArgumentDefaultException(string message, string paramName, Exception innerException) : base(message, paramName, innerException) { }

        protected ArgumentDefaultException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
