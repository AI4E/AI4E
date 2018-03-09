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
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
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
        private static readonly ConcurrentDictionary<Type, HandlerCacheEntry> _handlerTypeCache = new ConcurrentDictionary<Type, HandlerCacheEntry>();
        private static readonly ConcurrentDictionary<Type, ProcessorCacheEntry> _processorTypeCache = new ConcurrentDictionary<Type, ProcessorCacheEntry>();

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
                var cacheEntry = _processorTypeCache.GetOrAdd(messageProcessor.GetType(), processorType => new ProcessorCacheEntry(processorType));

                if (cacheEntry.CanSetContext)
                {
                    IMessageProcessorContext messageProcessorContext = new MessageProcessorContext(typeof(TMessage), _handler, _memberDescriptor, values);

                    cacheEntry.SetContext(messageProcessor, messageProcessorContext);
                }
                
                return (await messageProcessor.ProcessAsync(message, next)) ?? new SuccessDispatchResult();
            };
        }

        private async Task<IDispatchResult> InternalHandleAsync(TMessage message, DispatchValueDictionary values)
        {
            var cacheEntry = _handlerTypeCache.GetOrAdd(typeof(TMessage), messageType => new HandlerCacheEntry(messageType));

            if (cacheEntry.CanSetContext)
            {
                var context = new MessageDispatchContext(_serviceProvider, values);

                cacheEntry.SetContext(_handler, context);
            }

            if (cacheEntry.CanSetDispatcher)
            {
                var dispatcher = _serviceProvider.GetRequiredService<IMessageDispatcher>();

                cacheEntry.SetDispatcher(_handler, dispatcher);
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

        private readonly struct HandlerCacheEntry
        {
            private readonly Action<object, IMessageDispatchContext> _contextSetter;
            private readonly Action<object, IMessageDispatcher> _dispatcherSetter;

            public HandlerCacheEntry(Type handlerType) : this()
            {
                if (handlerType == null)
                    throw new ArgumentNullException(nameof(handlerType));

                var handlerParam = Expression.Parameter(typeof(object), "handler");
                var handlerConvert = Expression.Convert(handlerParam, handlerType);
                var contextProperty = handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                 .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageDispatchContext)) &&
                                                                      p.CanWrite &&
                                                                      p.GetIndexParameters().Length == 0 &&
                                                                      p.IsDefined<MessageDispatchContextAttribute>());
                var dispatcherProperty = handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                    .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageDispatcher)) &&
                                                                         p.CanWrite &&
                                                                         p.GetIndexParameters().Length == 0 &&
                                                                         p.IsDefined<MessageDispatcherAttribute>());
                if (contextProperty != null)
                {
                    var contextParam = Expression.Parameter(typeof(IMessageDispatchContext), "dispatchContext");
                    var propertyAccess = Expression.Property(handlerConvert, contextProperty);
                    var propertyAssign = Expression.Assign(propertyAccess, contextParam);
                    var lambda = Expression.Lambda<Action<object, IMessageDispatchContext>>(propertyAssign, handlerParam, contextParam);
                    _contextSetter = lambda.Compile();
                }

                if (dispatcherProperty != null)
                {
                    var dispatcherParam = Expression.Parameter(typeof(IMessageDispatcher), "messageDispatcher");
                    var propertyAccess = Expression.Property(handlerConvert, dispatcherProperty);
                    var propertyAssign = Expression.Assign(propertyAccess, dispatcherParam);
                    var lambda = Expression.Lambda<Action<object, IMessageDispatcher>>(propertyAssign, handlerParam, dispatcherParam);
                    _dispatcherSetter = lambda.Compile();
                }
            }

            public bool CanSetContext => _contextSetter != null;
            public bool CanSetDispatcher => _dispatcherSetter != null;

            public void SetContext(object handler, IMessageDispatchContext dispatchContext)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_contextSetter == null)
                    throw new InvalidOperationException();

                _contextSetter(handler, dispatchContext);
            }

            public void SetDispatcher(object handler, IMessageDispatcher messageDispatcher)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_contextSetter == null)
                    throw new InvalidOperationException();

                _dispatcherSetter(handler, messageDispatcher);
            }
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

        private readonly struct ProcessorCacheEntry
        {
            private readonly Action<object, IMessageProcessorContext> _contextSetter;

            public ProcessorCacheEntry(Type processorType) : this()
            {
                if (processorType == null)
                    throw new ArgumentNullException(nameof(processorType));

                var contextProperty = processorType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                   .FirstOrDefault(p => p.PropertyType.IsAssignableFrom(typeof(IMessageProcessorContext)) &&
                                                                        p.CanWrite &&
                                                                        p.GetIndexParameters().Length == 0 &&
                                                                        p.IsDefined<MessageProcessorContextAttribute>());
                if (contextProperty != null)
                {
                    var processorParam = Expression.Parameter(typeof(object), "processor");
                    var processorConvert = Expression.Convert(processorParam, processorType);
                    var contextParam = Expression.Parameter(typeof(IMessageProcessorContext), "processorContext");
                    var propertyAccess = Expression.Property(processorConvert, contextProperty);
                    var propertyAssign = Expression.Assign(propertyAccess, contextParam);
                    var lambda = Expression.Lambda<Action<object, IMessageProcessorContext>>(propertyAssign, processorParam, contextParam);
                    _contextSetter = lambda.Compile();
                }
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
