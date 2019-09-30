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

namespace AI4E.Messaging
{
    /// <summary>
    /// An attribute that marks the decorated type or member as message handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance of the <see cref="MessageDispatcherAttribute"/> type.
        /// </summary>
        public MessageHandlerAttribute() { }

        /// <summary>
        /// Creates a new instance of the <see cref="MessageDispatcherAttribute"/> type.
        /// </summary>
        /// <param name="messageType">The type of message handled.</param>
        public MessageHandlerAttribute(Type messageType)
        {
            MessageType = messageType;
        }

        /// <summary>
        /// Gets the type of message handled.
        /// </summary>
        public Type MessageType { get; }
    }
}
