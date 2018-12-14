using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public readonly struct MessageProcessorContextDescriptor
    {
        private static readonly ConcurrentDictionary<Type, MessageProcessorContextDescriptor> _descriptors
            = new ConcurrentDictionary<Type, MessageProcessorContextDescriptor>();

        public static MessageProcessorContextDescriptor GetDescriptor(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _descriptors.GetOrAdd(type, BuildDescriptor);
        }

        private static MessageProcessorContextDescriptor BuildDescriptor(Type type)
        {
            Assert(type != null);

            var contextProperty = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                             .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageProcessorContext)) &&
                                                                  p.CanWrite &&
                                                                  p.GetIndexParameters().Length == 0 &&
                                                                  p.IsDefined<MessageProcessorContextAttribute>());

            var contextSetter = contextProperty != null ? BuildContextSetter(type, contextProperty) : null;

            return new MessageProcessorContextDescriptor(contextSetter);
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

        private readonly Action<object, IMessageProcessorContext> _contextSetter;

        private MessageProcessorContextDescriptor(Action<object, IMessageProcessorContext> contextSetter)
        {
            _contextSetter = contextSetter;
        }

        public bool CanSetContext => _contextSetter != null;

        public void SetContext(object processor, IMessageProcessorContext processorContext)
        {
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));

            if (_contextSetter == null)
                throw new InvalidOperationException();

            _contextSetter(processor, processorContext);
        }
    }
}
