using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    public sealed class MessageHandlerInvoker<TMessage> : IMessageHandler<TMessage>
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

        Task<IDispatchResult> IMessageHandler<TMessage>.HandleAsync(TMessage message, DispatchValueDictionary dispatchValues)
        {
            return HandleAsync(message, dispatchValues, cancellation: default).AsTask();
        }

        public ValueTask<IDispatchResult> HandleAsync(TMessage message, DispatchValueDictionary dispatchValues, CancellationToken cancellation)
        {
            Func<TMessage, ValueTask<IDispatchResult>> next = (alteredMessage => InvokeHandlerCore(alteredMessage, dispatchValues, cancellation));

            for (var i = _processors.Length - 1; i >= 0; i--)
            {
                var processor = _processors[i].ProvideInstance(_serviceProvider);
                Assert(processor != null);
                next = (alteredMessage => InvokeProcessorAsync(processor, alteredMessage, dispatchValues, next, cancellation));
            }

            return next(message);
        }

        private ValueTask<IDispatchResult> InvokeProcessorAsync(IMessageProcessor processor,
                                                                TMessage message,
                                                                DispatchValueDictionary dispatchValue,
                                                                Func<TMessage, ValueTask<IDispatchResult>> next,
                                                                CancellationToken cancellation)
        {
            var contextDescriptor = MessageProcessorContextDescriptor.GetDescriptor(processor.GetType());

            if (contextDescriptor.CanSetContext)
            {
                IMessageProcessorContext messageProcessorContext = new MessageProcessorContext(typeof(TMessage), _handler, _memberDescriptor, dispatchValue);

                contextDescriptor.SetContext(processor, messageProcessorContext);
            }

            return new ValueTask<IDispatchResult>(processor.ProcessAsync(message, m => next(m).AsTask())); // TODO: Cancellation
        }

        private async ValueTask<IDispatchResult> InvokeHandlerCore(TMessage message, DispatchValueDictionary dispatchValues, CancellationToken cancellation)
        {
            IMessageDispatchContext context = null;
            var contextDescriptor = MessageHandlerContext.GetDescriptor(_handler.GetType());

            if (contextDescriptor.CanSetContext)
            {
                context = new MessageDispatchContext(_serviceProvider, dispatchValues);
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
                return new MessageDispatchContext(_serviceProvider, dispatchValues);
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
                else if (parameter.ParameterType == typeof(DispatchValueDictionary))
                {
                    return dispatchValues;
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
                result = await invoker.InvokeAsync(_handler, message, ResolveParameter);
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

                // https://github.com/AI4E/AI4E/issues/19
                return new NotFoundDispatchResult();
            }

            return SuccessDispatchResultBuilder.GetSuccessDispatchResult(invoker.ReturnTypeDescriptor.ResultType, result);
        }

        private sealed class MessageDispatchContext : IMessageDispatchContext
        {
            public MessageDispatchContext(IServiceProvider dispatchServices, DispatchValueDictionary dispatchValues)
            {
                if (dispatchServices == null)
                    throw new ArgumentNullException(nameof(dispatchServices));

                if (dispatchValues == null)
                    throw new ArgumentNullException(nameof(dispatchValues));

                DispatchServices = dispatchServices;
                DispatchValues = dispatchValues;
            }

            public IServiceProvider DispatchServices { get; }

            public DispatchValueDictionary DispatchValues { get; }
        }

        private sealed class MessageProcessorContext : IMessageProcessorContext
        {
            public MessageProcessorContext(Type messageType, object messageHandler, MessageHandlerActionDescriptor messageHandlerAction, DispatchValueDictionary dispatchValues)
            {
                if (messageHandler == null)
                    throw new ArgumentNullException(nameof(messageHandler));

                if (messageType == null)
                    throw new ArgumentNullException(nameof(messageType));

                if (dispatchValues == null)
                    throw new ArgumentNullException(nameof(dispatchValues));

                MessageHandler = messageHandler;
                MessageHandlerAction = messageHandlerAction;
                MessageType = messageType;
                DispatchValues = dispatchValues;
            }

            public object MessageHandler { get; }
            public MessageHandlerActionDescriptor MessageHandlerAction { get; }
            public Type MessageType { get; }
            public DispatchValueDictionary DispatchValues { get; }
        }
    }
}
