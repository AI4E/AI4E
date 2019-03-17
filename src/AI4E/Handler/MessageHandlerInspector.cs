using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AI4E.Utils;
using AI4E.Utils.Async;

namespace AI4E.Handler
{
    public sealed class MessageHandlerInspector
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

            // There is defined a NoMessageHandlerAttribute on the member somewhere in the inheritance hierarchy.
            if (member.IsDefined<NoMessageHandlerAttribute>(inherit: true))
            {
                // The member on the current type IS decorated with a NoMessageHandlerAttribute OR
                // The member on the current type IS NOT decorated with a MessageHandlerAttribute
                // TODO: Test this.
                if (member.IsDefined<NoMessageHandlerAttribute>(inherit: false) || !member.IsDefined<MessageHandlerAttribute>(inherit: false))
                {
                    descriptor = default;
                    return false;
                }
            }

            var messageType = parameters[0].ParameterType;

            var actionAttribute = member.GetCustomAttribute<MessageHandlerAttribute>(inherit: true);

            if (actionAttribute == null)
            {
                actionAttribute = _type.GetCustomAttribute<MessageHandlerAttribute>(inherit: true);
            }

            if (actionAttribute != null && actionAttribute.MessageType != null)
            {
                if (!messageType.IsAssignableFrom(actionAttribute.MessageType))
                {
                    throw new InvalidOperationException();
                }

                messageType = actionAttribute.MessageType;
            }

            var returnTypeDescriptor = AwaitableTypeDescriptor.GetTypeDescriptor(member.ReturnType);

            if (IsSychronousHandler(member, actionAttribute, returnTypeDescriptor) ||
                IsAsynchronousHandler(member, actionAttribute, returnTypeDescriptor))
            {
                descriptor = new MessageHandlerActionDescriptor(messageType, _type, member);
                return true;
            }

            descriptor = default;
            return false;
        }

        private static bool IsAsynchronousHandler(
            MethodInfo member,
            MessageHandlerAttribute actionAttribute,
            AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return (member.Name == "HandleAsync" || member.IsDefined<MessageHandlerAttribute>(inherit: true)) && returnTypeDescriptor.IsAwaitable;
        }

        private static bool IsSychronousHandler(
            MethodInfo member,
            MessageHandlerAttribute actionAttribute,
            AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return (member.Name == "Handle" || member.IsDefined<MessageHandlerAttribute>(inherit: true)) && !returnTypeDescriptor.IsAwaitable;
        }
    }
}
