using System;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class MessageHandlerProvider<TMessage> : IMessageHandlerFactory<TMessage>
        where TMessage : class
    {
        private readonly Type _handlerType;
        private readonly MessageHandlerActionDescriptor _actionDescriptor;
        private readonly ImmutableArray<IContextualProvider<IMessageProcessor>> _processors;

        public MessageHandlerProvider(
            Type handlerType,
            MessageHandlerActionDescriptor actionDescriptor,
            ImmutableArray<IContextualProvider<IMessageProcessor>> processors)
        {
            if (handlerType == null)
                throw new ArgumentNullException(nameof(handlerType));

            _handlerType = handlerType;
            _actionDescriptor = actionDescriptor;
            _processors = processors;
        }

        public IMessageHandler<TMessage> CreateMessageHandler(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return ProvideInstanceInternal(serviceProvider);
        }

        IMessageHandler IMessageHandlerFactory.CreateMessageHandler(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return ProvideInstanceInternal(serviceProvider);
        }

        Type IMessageHandlerFactory.MessageType => typeof(TMessage);

        private MessageHandlerInvoker<TMessage> ProvideInstanceInternal(IServiceProvider serviceProvider)
        {
            // Create a new instance of the handler type.
            var handler = ActivatorUtilities.CreateInstance(serviceProvider, _handlerType);

            Assert(handler != null);

            return new MessageHandlerInvoker<TMessage>(handler, _actionDescriptor, _processors, serviceProvider);
        }
    }
}
