/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Internal
{
    internal sealed class MessageHandlerInvoker<TMessage> : IMessageHandler<TMessage>
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

        public Task<IDispatchResult> HandleAsync(TMessage message, DispatchValueDictionary values)
        {
            return _processors.Reverse()
                                     .Aggregate(seed: (Func<TMessage, Task<IDispatchResult>>)(m => InternalHandleAsync(m, values)),
                                                func: (c, n) => WithNextProvider(n, c, values))
                                     .Invoke(message);
        }

        private Func<TMessage, Task<IDispatchResult>> WithNextProvider(IContextualProvider<IMessageProcessor> provider, Func<TMessage, Task<IDispatchResult>> next, DispatchValueDictionary values)
        {
            return async message =>
            {
                var messageProcessor = provider.ProvideInstance(_serviceProvider);
                Debug.Assert(messageProcessor != null);

                var props = messageProcessor.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var prop = props.FirstOrDefault(p => p.IsDefined<MessageProcessorContextAttribute>() &&
                                                     p.PropertyType == typeof(object) || p.PropertyType == typeof(IMessageProcessorContext) &&
                                                     p.GetIndexParameters().Length == 0 &&
                                                     p.CanWrite);

                if (prop != null)
                {
                    IMessageProcessorContext messageProcessorContext = new MessageProcessorContext(typeof(TMessage), _handler, _memberDescriptor, values);

                    prop.SetValue(messageProcessor, messageProcessorContext);
                }

                return (await messageProcessor.ProcessAsync(message, next)) ?? new SuccessDispatchResult();
            };
        }

        private async Task<IDispatchResult> InternalHandleAsync(TMessage message, DispatchValueDictionary values)
        {
            var contextProperty = _handler.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == typeof(MessageDispatchContext) &&
                                                                                         p.CanWrite &&
                                                                                         p.CanRead &&
                                                                                         p.GetIndexParameters().Length == 0 &&
                                                                                         p.IsDefined<MessageDispatchContextAttribute>());

            if (contextProperty != null)
            {
                var context = new MessageDispatchContext { DispatchServices = _serviceProvider, DispatchValues = values };

                contextProperty.SetValue(_handler, context);
            }

            var dispatcherProperty = _handler.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == typeof(IMessageDispatcher) &&
                                                                                            p.CanWrite &&
                                                                                            p.CanRead &&
                                                                                            p.GetIndexParameters().Length == 0 &&
                                                                                            p.IsDefined<MessageDispatcherAttribute>());

            if (dispatcherProperty != null)
            {
                dispatcherProperty.SetValue(_handler, _serviceProvider.GetRequiredService<IMessageDispatcher>());
            }

            var member = _memberDescriptor.Member;

            Debug.Assert(member != null);

            var parameters = member.GetParameters();

            var callingArgs = new object[parameters.Length];

            callingArgs[0] = message;

            for (var i = 1; i < callingArgs.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;

                object arg;

                if (parameterType.IsDefined<FromServicesAttribute>())
                {
                    arg = _serviceProvider.GetRequiredService(parameterType);
                }
                else
                {
                    arg = _serviceProvider.GetService(parameterType);

                    if (arg == null && parameterType.IsValueType)
                    {
                        arg = FormatterServices.GetUninitializedObject(parameterType);
                    }
                }

                callingArgs[i] = arg;
            }

            object result;
            Type returnType;

            try
            {
                result = member.Invoke(_handler, callingArgs);
            }
            catch (Exception exc)
            {
                return new FailureDispatchResult(exc);
            }

            if (member.ReturnType == typeof(void))
            {
                return new SuccessDispatchResult();
            }

            if (typeof(Task).IsAssignableFrom(member.ReturnType))
            {
                try
                {
                    await (Task)result;
                }
                catch (Exception exc)
                {
                    return new FailureDispatchResult(exc);
                }

                if (member.ReturnType == typeof(Task))
                {
                    return new SuccessDispatchResult();
                }

                // This only happens if the BCL changed.
                if (!member.ReturnType.IsGenericType)
                {
                    return new SuccessDispatchResult();
                }

                returnType = member.ReturnType.GetGenericArguments().First();
                result = (object)((dynamic)result).Result;
            }
            else
            {
                returnType = member.ReturnType;
            }

            if (result is IDispatchResult dispatchResult)
                return dispatchResult;

            if (result == null)
                return new FailureDispatchResult();

            return (IDispatchResult)Activator.CreateInstance(typeof(SuccessDispatchResult<>).MakeGenericType(returnType), result);
        }

        private sealed class MessageProcessorContext : IMessageProcessorContext
        {
            public MessageProcessorContext(Type messageType, object messageHandler, MessageHandlerActionDescriptor messageHandlerAction, DispatchValueDictionary dispatchValues)
            {
                Debug.Assert(dispatchValues != null);
                Debug.Assert(messageHandler != null);
                Debug.Assert(messageType != null);

                MessageType = messageType;
                MessageHandler = messageHandler;
                MessageHandlerAction = messageHandlerAction;
                DispatchValues = dispatchValues;
            }

            public object MessageHandler { get; }
            public MessageHandlerActionDescriptor MessageHandlerAction { get; }
            public Type MessageType { get; }

            public DispatchValueDictionary DispatchValues { get; }
        }
    }
}
