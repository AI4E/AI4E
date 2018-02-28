using System;
using System.Threading.Tasks;
using AI4E.Modularity.RPC;

namespace AI4E.Modularity.Debugging
{
    public sealed class MessageDispatcherSkeleton : IMessageDispatcherSkeleton
    {
        private readonly IRemoteMessageDispatcher _messageDispatcher;

        public MessageDispatcherSkeleton(IRemoteMessageDispatcher messageDispatcher)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _messageDispatcher = messageDispatcher;
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, EndPointRoute endPoint)
        {
            return _messageDispatcher.DispatchAsync(message, context, publish, endPoint);
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, EndPointRoute endPoint)
        {
            return _messageDispatcher.DispatchAsync(messageType, message, context, publish, endPoint);
        }

        public Task<IDispatchResult> DispatchLocalAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish)
        {
            return _messageDispatcher.DispatchLocalAsync(message, context, publish);
        }

        public Task<IDispatchResult> DispatchLocalAsync(Type messageType, object message, DispatchValueDictionary context, bool publish)
        {
            return _messageDispatcher.DispatchLocalAsync(messageType, message, context, publish);
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish)
        {
            return _messageDispatcher.DispatchAsync(message, context, publish);
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish)
        {
            return _messageDispatcher.DispatchAsync(messageType, message, context, publish);
        }

        public Proxy<IHandlerRegistration<IMessageHandler<TMessage>>> Register<TMessage>(Proxy<IProvider<Proxy<IMessageHandler<TMessage>>>> proxy)
        {
            var handler = new MessageHandlerProvider<TMessage>(proxy);
            var registration = _messageDispatcher.Register(handler);
            return new Proxy<IHandlerRegistration<IMessageHandler<TMessage>>>(registration);
        }

        private sealed class MessageHandlerProvider<TMessage> : IContextualProvider<IMessageHandler<TMessage>>
        {
            private readonly Proxy<IProvider<Proxy<IMessageHandler<TMessage>>>> _proxy;

            public MessageHandlerProvider(Proxy<IProvider<Proxy<IMessageHandler<TMessage>>>> proxy)
            {
                if (proxy == null)
                    throw new ArgumentNullException(nameof(proxy));

                _proxy = proxy;
            }

            public IMessageHandler<TMessage> ProvideInstance(IServiceProvider serviceProvider)
            {
                return new MessageHandler(_proxy.ExecuteAsync(p => p.ProvideInstance()));
            }

            private sealed class MessageHandler : IMessageHandler<TMessage>
            {
                private readonly Task<Proxy<IMessageHandler<TMessage>>> _proxyTask;

                public MessageHandler(Task<Proxy<IMessageHandler<TMessage>>> proxyTask)
                {
                    if (proxyTask == null)
                        throw new ArgumentNullException(nameof(proxyTask));

                    _proxyTask = proxyTask;
                }

                public async Task<IDispatchResult> HandleAsync(TMessage message, DispatchValueDictionary context)
                {
                    var proxy = await _proxyTask;

                    return await proxy.ExecuteAsync(p => p.HandleAsync(message, context));
                }
            }
        }
    }
}
