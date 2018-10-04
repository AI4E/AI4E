/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MessageHandlerActionDescriptor.cs 
 * Types:           AI4E.MessageHandlerActionDescriptor
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   01.06.2017 
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
using System.Reflection;

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
        /// <param name="member">A <see cref="MethodInfo"/> instance that specifies the member.</param>
        public MessageHandlerActionDescriptor(Type messageType, MethodInfo member)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (member == null)
                throw new ArgumentNullException(nameof(member));

            MessageType = messageType;
            Member = member;
        }

        /// <summary>
        /// Gets the type of message that is handled.
        /// </summary>
        public Type MessageType { get; }

        /// <summary>
        /// Gets a <see cref="MethodInfo"/> that specifies the message handler action (method).
        /// </summary>
        public MethodInfo Member { get; }
    }
}
