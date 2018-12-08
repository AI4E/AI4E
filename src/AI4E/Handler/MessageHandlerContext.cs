using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public static class MessageHandlerContext
    {
        private static readonly ConcurrentDictionary<Type, MessageHandlerContextDescriptor> _descriptors
            = new ConcurrentDictionary<Type, MessageHandlerContextDescriptor>();

        public static MessageHandlerContextDescriptor GetDescriptor(Type type)
        {
            return _descriptors.GetOrAdd(type, BuildDescriptor);
        }

        private static MessageHandlerContextDescriptor BuildDescriptor(Type type)
        {
            Assert(type != null);

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var handlerConvert = Expression.Convert(handlerParam, type);
            var contextProperty = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                             .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageDispatchContext)) &&
                                                                  p.CanWrite &&
                                                                  p.GetIndexParameters().Length == 0 &&
                                                                  p.IsDefined<MessageDispatchContextAttribute>());
            var dispatcherProperty = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageDispatcher)) &&
                                                                     p.CanWrite &&
                                                                     p.GetIndexParameters().Length == 0 &&
                                                                     p.IsDefined<MessageDispatcherAttribute>());

            Action<object, IMessageDispatchContext> contextSetter = null;
            Action<object, IMessageDispatcher> dispatcherSetter = null;

            if (contextProperty != null)
            {
                var contextParam = Expression.Parameter(typeof(IMessageDispatchContext), "dispatchContext");
                var propertyAccess = Expression.Property(handlerConvert, contextProperty);
                var propertyAssign = Expression.Assign(propertyAccess, contextParam);
                var lambda = Expression.Lambda<Action<object, IMessageDispatchContext>>(propertyAssign, handlerParam, contextParam);
                contextSetter = lambda.Compile();
            }

            if (dispatcherProperty != null)
            {
                var dispatcherParam = Expression.Parameter(typeof(IMessageDispatcher), "messageDispatcher");
                var propertyAccess = Expression.Property(handlerConvert, dispatcherProperty);
                var propertyAssign = Expression.Assign(propertyAccess, dispatcherParam);
                var lambda = Expression.Lambda<Action<object, IMessageDispatcher>>(propertyAssign, handlerParam, dispatcherParam);
                dispatcherSetter = lambda.Compile();
            }

            return new MessageHandlerContextDescriptor(contextSetter, dispatcherSetter);
        }
    }
}
