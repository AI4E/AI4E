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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AI4E.Internal
{
    internal sealed class MessageHandlerInspector
    {
        private readonly Type _type;

        public MessageHandlerInspector(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _type = type;
        }

        public IEnumerable<MessageHandlerActionDescriptor> GetHandlerDescriptors()
        {
            var members = _type.GetMethods();
            var descriptors = new List<MessageHandlerActionDescriptor>();

            foreach (var member in members)
            {
                if (TryGetHandlingMember(member, out var descriptor))
                {
                    descriptors.Add(descriptor);
                }
            }

            return descriptors;
        }

        private bool TryGetHandlingMember(MethodInfo member, out MessageHandlerActionDescriptor descriptor)
        {
            var parameters = member.GetParameters();

            if (parameters.Length == 0)
            {
                descriptor = default;
                return false;
            }

            if (parameters.Any(p => p.ParameterType.IsByRef))
            {
                descriptor = default;
                return false;
            }

            if (member.IsGenericMethod || member.IsGenericMethodDefinition)
            {
                descriptor = default;
                return false;
            }

            if (member.IsDefined<NoActionAttribute>())
            {
                descriptor = default;
                return false;
            }

            var messageType = parameters[0].ParameterType;

            var actionAttribute = member.GetCustomAttribute<ActionAttribute>();

            if (actionAttribute != null && actionAttribute.MessageType != null)
            {
                if (!messageType.IsAssignableFrom(actionAttribute.MessageType))
                {
                    throw new InvalidOperationException();
                }

                messageType = actionAttribute.MessageType;
            }

            // Synchronous handler
            if ((member.Name == "Handle" || actionAttribute != null) &&
                (member.ReturnType == typeof(void) || !typeof(Task).IsAssignableFrom(member.ReturnType)))
            {
                descriptor = new MessageHandlerActionDescriptor(messageType, member);
                return true;
            }

            // Asynchronous handler
            if ((member.Name == "HandleAsync" || actionAttribute != null) &&
                (typeof(Task).IsAssignableFrom(member.ReturnType)))
            {
                descriptor = new MessageHandlerActionDescriptor(messageType, member);
                return true;
            }

            descriptor = default;
            return false;
        }
    }
}
