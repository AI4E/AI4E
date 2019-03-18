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
using System.Linq;
using System.Reflection;
using AI4E.Utils;

namespace AI4E
{
    /// <summary>
    /// Describes a single message handler action (method).
    /// </summary>
    public readonly struct MessageHandlerActionDescriptor
    {
        /// <summary>
        /// Creates a new instance of the <see cref="MessageHandlerActionDescriptor"/> type.
        /// </summary>
        /// <param name="messageType">A <see cref="Type"/> that specifies the type of message.</param>
        /// <param name="messageHandlerType">A <see cref="Type"/> that specifies that shall be instanciated to create a message handler.</param>
        /// <param name="member">A <see cref="MethodInfo"/> instance that specifies the member.</param>
        public MessageHandlerActionDescriptor(Type messageType, Type messageHandlerType, MethodInfo member)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (messageHandlerType == null)
                throw new ArgumentNullException(nameof(messageHandlerType));

            if (member == null)
                throw new ArgumentNullException(nameof(member));

            if (!messageType.IsOrdinaryClass())
                throw new ArgumentException("The argument must specify an ordinary class.", nameof(messageType));

            if (!messageHandlerType.IsOrdinaryClass())
                throw new ArgumentException("The argument must specify an ordinary class.", nameof(messageHandlerType));

            if (member.IsGenericMethodDefinition || member.ContainsGenericParameters)
                throw new ArgumentException("The member must neither be an open method definition nor must it contain generic parameters.");

            var firstParameter = member.GetParameters().Select(p => p.ParameterType).FirstOrDefault();

            if (firstParameter == null)
                throw new ArgumentException("The member must not be parameterless", nameof(member));

            if (!firstParameter.IsAssignableFrom(messageType))
                throw new ArgumentException("The specified message type must be assignable the type of the members first parameter.");

            if (!member.DeclaringType.IsAssignableFrom(messageHandlerType))
                throw new ArgumentException("The specififed message handler type must be assignable to the type that declares the specified member.");

            // TODO: Do we also check whether any parameter/messageType/messageHandlerType is by ref or is a pointer, etc.

            MessageType = messageType;
            MessageHandlerType = messageHandlerType;
            Member = member;
        }

        /// <summary>
        /// Gets the type of message that is handled.
        /// </summary>
        public Type MessageType { get; }

        /// <summary>
        /// Gets the message handler type.
        /// </summary>
        public Type MessageHandlerType { get; }

        /// <summary>
        /// Gets a <see cref="MethodInfo"/> that specifies the message handler action (method).
        /// </summary>
        public MethodInfo Member { get; }
    }
}
