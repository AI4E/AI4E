using System;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;


namespace AI4E.Handler
{
    public sealed class MessageHandlerProvider<TMessage> : IContextualProvider<IMessageHandler<TMessage>>
        where TMessage : class
    {
        private readonly Type _type;
        private readonly MessageHandlerActionDescriptor _actionDescriptor;
        private readonly ImmutableArray<IContextualProvider<IMessageProcessor>> _processors;

        public MessageHandlerProvider(Type type, MessageHandlerActionDescriptor actionDescriptor, ImmutableArray<IContextualProvider<IMessageProcessor>> processors)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _type = type;
            _actionDescriptor = actionDescriptor;
            _processors = processors;
        }

        public IMessageHandler<TMessage> ProvideInstance(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            // Create a new instance of the handler type.
            var handler = ActivatorUtilities.CreateInstance(serviceProvider, _type);

            Assert(handler != null);

            return new MessageHandlerInvoker<TMessage>(handler, _actionDescriptor, _processors, serviceProvider);
        }
    }
}
