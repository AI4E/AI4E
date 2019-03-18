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
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    /// <summary>
    /// Represents a type descriptor used to set a message handler's context.
    /// </summary>
    public readonly struct MessageHandlerContextDescriptor
    {
        private static readonly ConcurrentDictionary<Type, MessageHandlerContextDescriptor> _descriptors
            = new ConcurrentDictionary<Type, MessageHandlerContextDescriptor>();

        /// <summary>
        /// Gets the <see cref="MessageHandlerContextDescriptor"/> for the specified type.
        /// </summary>
        /// <param name="type">The message handler type.</param>
        /// <returns>The <see cref="MessageHandlerContextDescriptor"/> for <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="type"/> is an open generic type definition.</exception>
        public static MessageHandlerContextDescriptor GetDescriptor(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _descriptors.GetOrAdd(type, BuildDescriptor);
        }

        private static MessageHandlerContextDescriptor BuildDescriptor(Type type)
        {
            Assert(type != null);

            if (type.IsAbstract)
                return default;

            if (type.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be an open generic type definition.", nameof(type));

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var contextProperty = GetContextProperty(type);
            var dispatcherProperty = GetDispatcherProperty(type);
            var convertedHandler = Expression.Convert(handlerParam, type);
            var contextSetter = contextProperty != null ? BuildContextSetter(handlerParam, convertedHandler, contextProperty) : null;
            var dispatcherSetter = dispatcherProperty != null ? BuildDispatcherSetter(handlerParam, convertedHandler, dispatcherProperty) : null;

            return new MessageHandlerContextDescriptor(type, contextSetter, dispatcherSetter);
        }

        private static PropertyInfo GetContextProperty(Type type)
        {
            return GetInstanceProperties(type).FirstOrDefault(p =>
                p.PropertyType.IsAssignableFrom(typeof(IMessageDispatchContext)) &&
                p.CanWrite &&
                p.GetIndexParameters().Length == 0 &&
                p.IsDefined<MessageDispatchContextAttribute>());
        }

        private static PropertyInfo GetDispatcherProperty(Type type)
        {
            return GetInstanceProperties(type).FirstOrDefault(p =>
                p.PropertyType.IsAssignableFrom(typeof(IMessageDispatcher)) &&
                p.CanWrite &&
                p.GetIndexParameters().Length == 0 &&
                p.IsDefined<MessageDispatcherAttribute>());
        }

        private static PropertyInfo[] GetInstanceProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

        private readonly Type _type;
        private readonly Action<object, IMessageDispatchContext> _contextSetter;
        private readonly Action<object, IMessageDispatcher> _dispatcherSetter;

        private MessageHandlerContextDescriptor(Type type,
                                                Action<object, IMessageDispatchContext> contextSetter,
                                                Action<object, IMessageDispatcher> dispatcherSetter)
        {
            _type = type;
            _contextSetter = contextSetter;
            _dispatcherSetter = dispatcherSetter;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the dispatch context can be set.
        /// </summary>
        public bool CanSetContext => _contextSetter != null;

        /// <summary>
        /// Gets a boolean value indicating whether the message dispatcher can be set.
        /// </summary>
        public bool CanSetDispatcher => _dispatcherSetter != null;

        /// <summary>
        /// Sets the dispatch context to the specified message handler.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <param name="dispatchContext">The dispatch context.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="CanSetContext"/> is false.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="handler"/> is of a different type then the descriptor was build for.
        /// </exception>
        public void SetContext(object handler, IMessageDispatchContext dispatchContext)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_contextSetter == null)
                throw new InvalidOperationException();

            if (handler.GetType() != _type)
                throw new ArgumentException($"The argument must be of type {_type}.", nameof(handler));

            _contextSetter(handler, dispatchContext);
        }


        /// <summary>
        /// Sets the message dispatcher to the specified message handler.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="CanSetDispatcher"/> is false.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="handler"/> is of a different type then the descriptor was build for.
        /// </exception>
        public void SetDispatcher(object handler, IMessageDispatcher messageDispatcher)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_dispatcherSetter == null)
                throw new InvalidOperationException();

            if (handler.GetType() != _type)
                throw new ArgumentException($"The argument must be of type {_type}.", nameof(handler));

            _dispatcherSetter(handler, messageDispatcher);
        }
    }
}
