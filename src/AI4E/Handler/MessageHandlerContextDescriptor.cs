using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public readonly struct MessageHandlerContextDescriptor
    {
        private static readonly ConcurrentDictionary<Type, MessageHandlerContextDescriptor> _descriptors
            = new ConcurrentDictionary<Type, MessageHandlerContextDescriptor>();

        public static MessageHandlerContextDescriptor GetDescriptor(Type handlerType)
        {
            return _descriptors.GetOrAdd(handlerType, BuildDescriptor);
        }

        private static MessageHandlerContextDescriptor BuildDescriptor(Type handlerType)
        {
            Assert(handlerType != null);

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var convertedHandler = Expression.Convert(handlerParam, handlerType);
            var contextProperty = handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                             .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageDispatchContext)) &&
                                                                  p.CanWrite &&
                                                                  p.GetIndexParameters().Length == 0 &&
                                                                  p.IsDefined<MessageDispatchContextAttribute>());
            var dispatcherProperty = handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageDispatcher)) &&
                                                                     p.CanWrite &&
                                                                     p.GetIndexParameters().Length == 0 &&
                                                                     p.IsDefined<MessageDispatcherAttribute>());

            var contextSetter = contextProperty != null ? BuildContextSetter(handlerParam, convertedHandler, contextProperty) : null;
            var dispatcherSetter = dispatcherProperty != null ? BuildDispatcherSetter(handlerParam, convertedHandler, dispatcherProperty) : null;

            return new MessageHandlerContextDescriptor(contextSetter, dispatcherSetter);
        }

        private static Action<object, IMessageDispatchContext> BuildContextSetter(
            ParameterExpression handlerParam,
            UnaryExpression convertedHandler,
            PropertyInfo contextProperty)
        {
            var contextParam = Expression.Parameter(typeof(IMessageDispatchContext), "dispatchContext");
            var propertyAccess = Expression.Property(convertedHandler, contextProperty);
            var propertyAssign = Expression.Assign(propertyAccess, contextParam);
            var lambda = Expression.Lambda<Action<object, IMessageDispatchContext>>(propertyAssign, handlerParam, contextParam);
            return lambda.Compile();
        }

        private static Action<object, IMessageDispatcher> BuildDispatcherSetter(
            ParameterExpression handlerParam,
            UnaryExpression convertedHandler,
            PropertyInfo dispatcherProperty)
        {
            var dispatcherParam = Expression.Parameter(typeof(IMessageDispatcher), "messageDispatcher");
            var propertyAccess = Expression.Property(convertedHandler, dispatcherProperty);
            var propertyAssign = Expression.Assign(propertyAccess, dispatcherParam);
            var lambda = Expression.Lambda<Action<object, IMessageDispatcher>>(propertyAssign, handlerParam, dispatcherParam);
            return lambda.Compile();
        }

        private readonly Action<object, IMessageDispatchContext> _contextSetter;
        private readonly Action<object, IMessageDispatcher> _dispatcherSetter;

        private MessageHandlerContextDescriptor(Action<object, IMessageDispatchContext> contextSetter,
                                                Action<object, IMessageDispatcher> dispatcherSetter)
        {
            _contextSetter = contextSetter;
            _dispatcherSetter = dispatcherSetter;
        }

        public bool CanSetContext => _contextSetter != null;
        public bool CanSetDispatcher => _dispatcherSetter != null;

        public void SetContext(object handler, IMessageDispatchContext dispatchContext)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_contextSetter == null)
                throw new InvalidOperationException();

            _contextSetter(handler, dispatchContext);
        }

        public void SetDispatcher(object handler, IMessageDispatcher messageDispatcher)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_contextSetter == null)
                throw new InvalidOperationException();

            _dispatcherSetter(handler, messageDispatcher);
        }
    }
}
