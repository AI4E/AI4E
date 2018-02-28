using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.RPC;

namespace AI4E.Modularity.Debugging
{
    public sealed class DebugMessageDispatcher : IRemoteMessageDispatcher
    {
        private readonly IProxy<IMessageDispatcherSkeleton> _proxy;
        private readonly IServiceProvider _serviceProvider;

        public DebugMessageDispatcher(IProxy<IMessageDispatcherSkeleton> proxy, EndPointRoute localEndPoint, IServiceProvider serviceProvider)
        {
            if (proxy == null)
                throw new ArgumentNullException(nameof(proxy));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _proxy = proxy;
            LocalEndPoint = localEndPoint;
            _serviceProvider = serviceProvider;
        }

        public EndPointRoute LocalEndPoint { get; }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message,
                                                             DispatchValueDictionary context,
                                                             bool publish,
                                                             EndPointRoute endPoint,
                                                             CancellationToken cancellation = default)
        {
            return _proxy.ExecuteAsync(dispatcher => dispatcher.DispatchAsync(message, context, publish, endPoint));
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType,
                                                   object message,
                                                   DispatchValueDictionary context,
                                                   bool publish,
                                                   EndPointRoute endPoint,
                                                   CancellationToken cancellation = default)
        {
            return _proxy.ExecuteAsync(dispatcher => dispatcher.DispatchAsync(messageType, message, context, publish, endPoint));
        }

        public Task<IDispatchResult> DispatchLocalAsync<TMessage>(TMessage message,
                                                                  DispatchValueDictionary context,
                                                                  bool publish,
                                                                  CancellationToken cancellation = default)
        {
            return _proxy.ExecuteAsync(dispatcher => dispatcher.DispatchLocalAsync(message, context, publish));
        }

        public Task<IDispatchResult> DispatchLocalAsync(Type messageType,
                                                        object message,
                                                        DispatchValueDictionary context,
                                                        bool publish,
                                                        CancellationToken cancellation = default)
        {
            return _proxy.ExecuteAsync(dispatcher => dispatcher.DispatchLocalAsync(messageType, message, context, publish));
        }



        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message,
                                                             DispatchValueDictionary context,
                                                             bool publish,
                                                             CancellationToken cancellation = default)
        {
            return _proxy.ExecuteAsync(dispatcher => dispatcher.DispatchAsync(message, context, publish));
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType,
                                                   object message,
                                                   DispatchValueDictionary context,
                                                   bool publish,
                                                   CancellationToken cancellation = default)
        {
            return _proxy.ExecuteAsync(dispatcher => dispatcher.DispatchAsync(messageType, message, context, publish));
        }

        public IHandlerRegistration<IMessageHandler<TMessage>> Register<TMessage>(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
        {
            var handlerProxy = new Proxy<IProvider<Proxy<IMessageHandler<TMessage>>>>(new MessageHandlerProvider<TMessage>(messageHandlerProvider, _serviceProvider));
            var registrationProxy = _proxy.ExecuteAsync(dispatcher => dispatcher.Register(handlerProxy));

            return new HandlerRegistration<TMessage>(messageHandlerProvider, registrationProxy);
        }

        private sealed class MessageHandlerProvider<TMessage> : IProvider<Proxy<IMessageHandler<TMessage>>>
        {
            private readonly IContextualProvider<IMessageHandler<TMessage>> _messageHandlerProvider;
            private readonly IServiceProvider _serviceProvider;

            public MessageHandlerProvider(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider, IServiceProvider serviceProvider)
            {
                if (messageHandlerProvider == null)
                    throw new ArgumentNullException(nameof(messageHandlerProvider));

                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                _messageHandlerProvider = messageHandlerProvider;
                _serviceProvider = serviceProvider;
            }

            public Proxy<IMessageHandler<TMessage>> ProvideInstance()
            {
                return new Proxy<IMessageHandler<TMessage>>(_messageHandlerProvider.ProvideInstance(_serviceProvider));
            }
        }

        private sealed class HandlerRegistration<TMessage> : IHandlerRegistration<IMessageHandler<TMessage>>
        {
            private readonly Task<Proxy<IHandlerRegistration<IMessageHandler<TMessage>>>> _proxyTask;

            public HandlerRegistration(IContextualProvider<IMessageHandler<TMessage>> handler,
                                       Task<Proxy<IHandlerRegistration<IMessageHandler<TMessage>>>> proxyTask)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (proxyTask == null)
                    throw new ArgumentNullException(nameof(proxyTask));

                Handler = handler;
                _proxyTask = proxyTask;
            }

            public IContextualProvider<IMessageHandler<TMessage>> Handler { get; }

            public Task Cancellation => GetCancellation();

            private async Task GetCancellation()
            {
                await (await _proxyTask).ExecuteAsync(p => p.Cancellation);
            }

            public async void Cancel() // TODO: What can we do with the task?
            {
                await (await _proxyTask).ExecuteAsync(p => p.Cancel());
            }
        }
    }
}
