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

namespace AI4E.Utils
{
    [Serializable]
    public class ContextUnloadedException : Exception
    {
        public ContextUnloadedException() : base("The context that the requested resource was loaded from is unloaded.")
        {
        }

        public ContextUnloadedException(string? message) : base(message)
        {
        }

        public ContextUnloadedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected ContextUnloadedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}