using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class MessageProcessorContextDescriptor
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

            Action<object, IMessageProcessorContext> contextSetter = null;

            if (contextProperty != null)
            {
                var processorParam = Expression.Parameter(typeof(object), "processor");
                var processorConvert = Expression.Convert(processorParam, type);
                var contextParam = Expression.Parameter(typeof(IMessageProcessorContext), "processorContext");
                var propertyAccess = Expression.Property(processorConvert, contextProperty);
                var propertyAssign = Expression.Assign(propertyAccess, contextParam);
                var lambda = Expression.Lambda<Action<object, IMessageProcessorContext>>(propertyAssign, processorParam, contextParam);
                contextSetter = lambda.Compile();
            }

            return new MessageProcessorContextDescriptor(contextSetter);
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
