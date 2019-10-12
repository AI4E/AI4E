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
using System.Collections.Generic;
using System.Reflection;
using AI4E.Utils.Async;

namespace AI4E.Messaging.MessageHandlers
{
    /// <summary>
    /// Inspects the members of message handlers.
    /// </summary>
    public sealed class MessageHandlerInspector : TypeMemberInspector<MessageHandlerActionDescriptor>
    {
        [ThreadStatic] private static MessageHandlerInspector? _instance;

        /// <summary>
        /// Gets the singleton <see cref="MessageHandlerInspector"/> instance for the current thread.
        /// </summary>
        public static MessageHandlerInspector Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MessageHandlerInspector();

                return _instance;
            }
        }

        private MessageHandlerInspector() { }

        /// <inheritdoc/>
        protected override bool IsSychronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return member.Name == "Handle" || member.IsDefined<MessageHandlerAttribute>(inherit: true);
        }

        /// <inheritdoc/>
        protected override bool IsAsynchronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return member.Name == "HandleAsync" || member.IsDefined<MessageHandlerAttribute>(inherit: true);
        }

        /// <inheritdoc/>
        protected override MessageHandlerActionDescriptor CreateDescriptor(Type type, MethodInfo member, Type parameterType)
        {
            return new MessageHandlerActionDescriptor(parameterType, type, member);
        }

        /// <inheritdoc/>
        protected override bool IsValidMember(MethodInfo member)
        {
            if (!base.IsValidMember(member))
                return false;

            // There is defined a NoMessageHandlerAttribute on the member somewhere in the inheritance hierarchy.
            if (member.IsDefined<NoMessageHandlerAttribute>(inherit: true))
            {
                // The member on the current type IS decorated with a NoMessageHandlerAttribute OR
                // The member on the current type IS NOT decorated with a MessageHandlerAttribute
                // TODO: Test this.
                if (member.IsDefined<NoMessageHandlerAttribute>(inherit: false) || !member.IsDefined<MessageHandlerAttribute>(inherit: false))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        protected override Type GetParameters(Type type, MethodInfo member, IReadOnlyList<ParameterInfo> parameters, AwaitableTypeDescriptor returnTypeDescriptor)
        {
            var parameterType = base.GetParameters(type, member, parameters, returnTypeDescriptor);

            var actionAttribute = member.GetCustomAttribute<MessageHandlerAttribute>(inherit: true);

            if (actionAttribute == null)
            {
                actionAttribute = type.GetCustomAttribute<MessageHandlerAttribute>(inherit: true);
            }

            if (actionAttribute != null && actionAttribute.MessageType != null)
            {
                if (!parameterType.IsAssignableFrom(actionAttribute.MessageType))
                {
                    throw new InvalidOperationException();
                }

                parameterType = actionAttribute.MessageType;
            }

            return parameterType;
        }
    }
}
