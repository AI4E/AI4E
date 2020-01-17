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
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AI4E.Messaging.MessageHandlers
{
    /// <summary>
    /// Represents a type descriptor used to set a message processors's context.
    /// </summary>
    public sealed class MessageProcessorContextDescriptor
    {
        private static readonly ConditionalWeakTable<Type, MessageProcessorContextDescriptor> _descriptors
            = new ConditionalWeakTable<Type, MessageProcessorContextDescriptor>();

        private static readonly ConditionalWeakTable<Type, MessageProcessorContextDescriptor>.CreateValueCallback _buildDescriptor
            = BuildDescriptor; // Cache delegate for perf reasons

        /// <summary>
        /// Gets the <see cref="MessageProcessorContextDescriptor"/> for the specified type.
        /// </summary>
        /// <param name="type">The processor type.</param>
        /// <returns>The <see cref="MessageProcessorContextDescriptor"/> for <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="type"/> is an open generic type definition.</exception>
        public static MessageProcessorContextDescriptor GetDescriptor(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _descriptors.GetValue(type, _buildDescriptor);
        }

        private static MessageProcessorContextDescriptor BuildDescriptor(Type type)
        {
            Debug.Assert(type != null);

            if (type!.IsAbstract)
                return new MessageProcessorContextDescriptor(type, null);

            if (type.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be an open generic type definition.", nameof(type));

            var contextProperty = GetContextProperty(type);
            var contextSetter = contextProperty != null ? BuildContextSetter(type, contextProperty) : null;

            return new MessageProcessorContextDescriptor(type, contextSetter);
        }

        private static PropertyInfo GetContextProperty(Type type)
        {
            return GetInstanceProperty(type).FirstOrDefault(p =>
                p.PropertyType.IsAssignableFrom(typeof(IMessageProcessorContext)) &&
                p.CanWrite &&
                p.GetIndexParameters().Length == 0 &&
                p.IsDefined<MessageProcessorContextAttribute>());
        }

        private static PropertyInfo[] GetInstanceProperty(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Action<object, IMessageProcessorContext> BuildContextSetter(Type type, PropertyInfo contextProperty)
        {
            var processorParam = Expression.Parameter(typeof(object), "processor");
            var convertedProcessor = Expression.Convert(processorParam, type);
            var contextParam = Expression.Parameter(typeof(IMessageProcessorContext), "processorContext");
            var propertyAccess = Expression.Property(convertedProcessor, contextProperty);
            var propertyAssign = Expression.Assign(propertyAccess, contextParam);
            var lambda = Expression.Lambda<Action<object, IMessageProcessorContext>>(propertyAssign, processorParam, contextParam);
            return lambda.Compile();
        }

        private readonly Type _type;
        private readonly Action<object, IMessageProcessorContext>? _contextSetter;

        private MessageProcessorContextDescriptor(Type type, Action<object, IMessageProcessorContext>? contextSetter)
        {
            _type = type;
            _contextSetter = contextSetter;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the processor context can be set.
        /// </summary>
        public bool CanSetContext => _contextSetter != null;

        /// <summary>
        /// Sets the processor context to the specified processor.
        /// </summary>
        /// <param name="processor">The message processor.</param>
        /// <param name="processorContext">The message processor context.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="processor"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="CanSetContext"/> is false.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="processor"/> is of a different type then the descriptor was build for.</exception>
        public void SetContext(object processor, IMessageProcessorContext processorContext)
        {
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));

            if (_contextSetter == null)
                throw new InvalidOperationException();

            if (processor.GetType() != _type)
                throw new ArgumentException($"The argument must be of type {_type}.", nameof(processor));

            _contextSetter(processor, processorContext);
        }
    }
}
