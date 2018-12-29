using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class MessageHandlerInvoker<TMessage> : IMessageHandler<TMessage>, IMessageHandler
        where TMessage : class
    {
        private readonly object _handler;
        private readonly MessageHandlerActionDescriptor _memberDescriptor;
        private readonly ImmutableArray<IContextualProvider<IMessageProcessor>> _processors;
        private readonly IServiceProvider _serviceProvider;

        public MessageHandlerInvoker(object handler,
                                     MessageHandlerActionDescriptor memberDescriptor,
                                     ImmutableArray<IContextualProvider<IMessageProcessor>> processors,
                                     IServiceProvider serviceProvider)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _handler = handler;
            _memberDescriptor = memberDescriptor;
            _processors = processors;
            _serviceProvider = serviceProvider;
        }

        public ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary<TMessage> dispatchData, bool publish, CancellationToken cancellation)
        {
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next = (nextDispatchData => InvokeHandlerCore(nextDispatchData, publish, cancellation));

            for (var i = _processors.Length - 1; i >= 0; i--)
            {
                var processor = _processors[i].ProvideInstance(_serviceProvider);
                Assert(processor != null);
                var nextCopy = next; // This is needed because of the way, the variable values are captured in the lambda expression.

                next = (nextDispatchData => InvokeProcessorAsync(processor, nextDispatchData, publish, nextCopy, cancellation));
            }

            return next(dispatchData);
        }

        public ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation)
        {
            if (!(dispatchData.Message is TMessage))
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{MessageType}'.");

            if (!(dispatchData is DispatchDataDictionary<TMessage> typedDispatchData))
            {
                typedDispatchData = new DispatchDataDictionary<TMessage>(dispatchData.Message as TMessage, dispatchData);
            }

            return HandleAsync(typedDispatchData, publish, cancellation);
        }

        public Type MessageType => typeof(TMessage);

        private ValueTask<IDispatchResult> InvokeProcessorAsync(IMessageProcessor processor,
                                                                DispatchDataDictionary<TMessage> dispatchData,
                                                                bool publish,
                                                                Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
                                                                CancellationToken cancellation)
        {
            var contextDescriptor = MessageProcessorContextDescriptor.GetDescriptor(processor.GetType());

            if (contextDescriptor.CanSetContext)
            {
                IMessageProcessorContext messageProcessorContext = new MessageProcessorContext(_handler, _memberDescriptor, publish);

                contextDescriptor.SetContext(processor, messageProcessorContext);
            }

            return processor.ProcessAsync(dispatchData, next, cancellation);
        }

        private async ValueTask<IDispatchResult> InvokeHandlerCore(DispatchDataDictionary<TMessage> dispatchData, bool publish, CancellationToken cancellation)
        {
            IMessageDispatchContext context = null;
            var contextDescriptor = MessageHandlerContextDescriptor.GetDescriptor(_handler.GetType());

            if (contextDescriptor.CanSetContext)
            {
                context = new MessageDispatchContext(_serviceProvider, dispatchData, publish);
                contextDescriptor.SetContext(_handler, context);
            }

            if (contextDescriptor.CanSetDispatcher)
            {
                var dispatcher = _serviceProvider.GetRequiredService<IMessageDispatcher>();
                contextDescriptor.SetDispatcher(_handler, dispatcher);
            }

            var member = _memberDescriptor.Member;
            Assert(member != null);
            var invoker = HandlerActionInvoker.GetInvoker(member);

            IMessageDispatchContext BuildContext()
            {
                return new MessageDispatchContext(_serviceProvider, dispatchData, publish);
            }

            object ResolveParameter(ParameterInfo parameter)
            {
                if (parameter.ParameterType == typeof(IServiceProvider))
                {
                    return _serviceProvider;
                }
                else if (parameter.ParameterType == typeof(CancellationToken))
                {
                    return cancellation;
                }
                else if (parameter.ParameterType == typeof(IMessageDispatchContext))
                {
                    if (context == null)
                    {
                        context = BuildContext();
                    }

                    return context;
                }
                else if (parameter.ParameterType == typeof(DispatchDataDictionary) || parameter.ParameterType == typeof(DispatchDataDictionary<TMessage>))
                {
                    return dispatchData;
                }
                else if (parameter.IsDefined<InjectAttribute>())
                {
                    return _serviceProvider.GetRequiredService(parameter.ParameterType);
                }
                else
                {
                    return _serviceProvider.GetService(parameter.ParameterType);
                }
            }

            object result;

            try
            {
                result = await invoker.InvokeAsync(_handler, dispatchData.Message, ResolveParameter);
            }
            catch (Exception exc)
            {
                return new FailureDispatchResult(exc);
            }

            if (result == null)
            {
                if (invoker.ReturnTypeDescriptor.ResultType == typeof(void))
                {
                    return new SuccessDispatchResult();
                }

                return new NotFoundDispatchResult();
            }

            if (result is IDispatchResult dispatchResult)
            {
                return dispatchResult;
            }

            return SuccessDispatchResult.FromResult(invoker.ReturnTypeDescriptor.ResultType, result);
        }

        private sealed class MessageDispatchContext : IMessageDispatchContext
        {
            public MessageDispatchContext(IServiceProvider dispatchServices, DispatchDataDictionary dispatchData, bool publish)
            {
                if (dispatchServices == null)
                    throw new ArgumentNullException(nameof(dispatchServices));

                if (dispatchData == null)
                    throw new ArgumentNullException(nameof(dispatchData));

                DispatchServices = dispatchServices;
                DispatchData = dispatchData;
                Publish = publish;
            }

            public IServiceProvider DispatchServices { get; }
            public DispatchDataDictionary DispatchData { get; }
            public bool Publish { get; }
        }

        private sealed class MessageProcessorContext : IMessageProcessorContext
        {
            public MessageProcessorContext(object messageHandler, MessageHandlerActionDescriptor messageHandlerAction, bool publish)
            {
                if (messageHandler == null)
                    throw new ArgumentNullException(nameof(messageHandler));

                MessageHandler = messageHandler;
                MessageHandlerAction = messageHandlerAction;
                Publish = publish;
            }

            public object MessageHandler { get; }
            public MessageHandlerActionDescriptor MessageHandlerAction { get; }
            public bool Publish { get; }
        }
    }
}
