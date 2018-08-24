using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.ApplicationParts;
using AI4E.DispatchResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;


namespace AI4E.Internal
{
    internal static class MessageDispatcherBuilder
    {
        public static TMessageDispatcher BuildMessageDispatcher<TMessageDispatcher>(IServiceProvider serviceProvider, TMessageDispatcher messageDispatcher)
            where TMessageDispatcher : class, IMessageDispatcher
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            var options = serviceProvider.GetService<IOptions<MessagingOptions>>()?.Value ?? new MessagingOptions();
            var processors = options.MessageProcessors.ToImmutableArray();
            var partManager = serviceProvider.GetRequiredService<ApplicationPartManager>();
            var messageHandlerFeature = new MessageHandlerFeature();

            partManager.PopulateFeature(messageHandlerFeature);
            RegisterMessageHandlerTypes(messageDispatcher, processors, messageHandlerFeature.MessageHandlers);

            return messageDispatcher;
        }

        private static void RegisterMessageHandlerTypes(IMessageDispatcher messageDispatcher,
                                                        ImmutableArray<IContextualProvider<IMessageProcessor>> processors,
                                                        IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                RegisterMessageHandlerType(messageDispatcher, processors, type);
            }
        }

        private static void RegisterMessageHandlerType(IMessageDispatcher messageDispatcher,
                                                       ImmutableArray<IContextualProvider<IMessageProcessor>> processors,
                                                       Type type)
        {
            var inspector = new MessageHandlerInspector(type);
            var descriptors = inspector.GetHandlerDescriptors();

            foreach (var descriptor in descriptors)
            {
                var messageType = descriptor.MessageType;
                var provider = Activator.CreateInstance(typeof(MessageHandlerProvider<>).MakeGenericType(messageType),
                                                        type,
                                                        descriptor,
                                                        processors);

                var registerMethodDefinition = typeof(IMessageDispatcher).GetMethods()
                    .Single(p => p.Name == "Register" && p.IsGenericMethodDefinition && p.GetGenericArguments().Length == 1);

                var registerMethod = registerMethodDefinition.MakeGenericMethod(messageType);

                registerMethod.Invoke(messageDispatcher, new object[] { provider });
            }
        }

        public static void ConfigureFeatureProviders(ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<MessageHandlerFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new MessageHandlerFeatureProvider());
            }
        }

        private sealed class MessageHandlerInspector
        {
            private readonly Type _type;

            public MessageHandlerInspector(Type type)
            {
                if (type == null)
                    throw new ArgumentNullException(nameof(type));

                _type = type;
            }

            public IEnumerable<MessageHandlerActionDescriptor> GetHandlerDescriptors()
            {
                var members = _type.GetMethods();
                var descriptors = new List<MessageHandlerActionDescriptor>();

                foreach (var member in members)
                {
                    if (TryGetHandlingMember(member, out var descriptor))
                    {
                        descriptors.Add(descriptor);
                    }
                }

                return descriptors;
            }

            private bool TryGetHandlingMember(MethodInfo member, out MessageHandlerActionDescriptor descriptor)
            {
                var parameters = member.GetParameters();

                if (parameters.Length == 0)
                {
                    descriptor = default;
                    return false;
                }

                if (parameters.Any(p => p.ParameterType.IsByRef))
                {
                    descriptor = default;
                    return false;
                }

                if (member.IsGenericMethod || member.IsGenericMethodDefinition)
                {
                    descriptor = default;
                    return false;
                }

                if (member.IsDefined<NoActionAttribute>())
                {
                    descriptor = default;
                    return false;
                }

                var messageType = parameters[0].ParameterType;

                var actionAttribute = member.GetCustomAttribute<ActionAttribute>();

                if (actionAttribute != null && actionAttribute.MessageType != null)
                {
                    if (!messageType.IsAssignableFrom(actionAttribute.MessageType))
                    {
                        throw new InvalidOperationException();
                    }

                    messageType = actionAttribute.MessageType;
                }

                // Synchronous handler
                if ((member.Name == "Handle" || actionAttribute != null) &&
                    (member.ReturnType == typeof(void) || !typeof(Task).IsAssignableFrom(member.ReturnType)))
                {
                    descriptor = new MessageHandlerActionDescriptor(messageType, member);
                    return true;
                }

                // Asynchronous handler
                if ((member.Name == "HandleAsync" || actionAttribute != null) &&
                    (typeof(Task).IsAssignableFrom(member.ReturnType)))
                {
                    descriptor = new MessageHandlerActionDescriptor(messageType, member);
                    return true;
                }

                descriptor = default;
                return false;
            }
        }

        private sealed class MessageHandlerInvoker<TMessage> : IMessageHandler<TMessage>
        {
            private static readonly ConcurrentDictionary<Type, HandlerCacheEntry> _handlerTypeCache = new ConcurrentDictionary<Type, HandlerCacheEntry>();
            private static readonly ConcurrentDictionary<Type, ProcessorCacheEntry> _processorTypeCache = new ConcurrentDictionary<Type, ProcessorCacheEntry>();
            private static readonly ConcurrentDictionary<MemberInfo, HandlerMemberCacheEntry> _handlerMemberCache = new ConcurrentDictionary<MemberInfo, HandlerMemberCacheEntry>();

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
                    Assert(messageProcessor != null);
                    var cacheEntry = _processorTypeCache.GetOrAdd(messageProcessor.GetType(), processorType => new ProcessorCacheEntry(processorType));

                    if (cacheEntry.CanSetContext)
                    {
                        IMessageProcessorContext messageProcessorContext = new MessageProcessorContext(typeof(TMessage), _handler, _memberDescriptor, values);

                        cacheEntry.SetContext(messageProcessor, messageProcessorContext);
                    }

                    return (await messageProcessor.ProcessAsync(message, next)) ?? new SuccessDispatchResult();
                };
            }

            private Task<IDispatchResult> InternalHandleAsync(TMessage message, DispatchValueDictionary values)
            {
                var cacheEntry = _handlerTypeCache.GetOrAdd(_handler.GetType(), messageType => new HandlerCacheEntry(messageType));

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

                Assert(member != null);

                var memberCacheEntry = _handlerMemberCache.GetOrAdd(member, _ => new HandlerMemberCacheEntry(member));

                return memberCacheEntry.Invoke(_handler, message, _serviceProvider);
            }

            private readonly struct HandlerMemberCacheEntry
            {
                private static readonly MethodInfo _getServiceMethod = typeof(ServiceProviderServiceExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public)
                                                                                                               .SingleOrDefault(p => p.Name == nameof(ServiceProviderServiceExtensions.GetRequiredService) &&
                                                                                                                                    !p.IsGenericMethodDefinition);
                private readonly ConstructorInfo _createTypedSuccessDispatchResult;
                private readonly Func<object, object, IServiceProvider, Task<IDispatchResult>> _invoker;
                private readonly MethodInfo _methodInfo;
                private readonly Type _returnType;

                public HandlerMemberCacheEntry(MethodInfo methodInfo) : this()
                {
                    if (methodInfo == null)
                        throw new ArgumentNullException(nameof(methodInfo));

                    if (methodInfo.IsGenericMethodDefinition)
                        throw new ArgumentException();

                    _methodInfo = methodInfo;
                    var returnType = methodInfo.ReturnType;

                    if (typeof(Task).IsAssignableFrom(returnType))
                    {
                        if (returnType.IsConstructedGenericType)
                        {
                            returnType = returnType.GetGenericArguments().First();
                        }
                        else
                        {
                            returnType = typeof(void);
                        }
                    }

                    _returnType = returnType;

                    if (returnType != typeof(void))
                    {
                        _createTypedSuccessDispatchResult = typeof(SuccessDispatchResult<>).MakeGenericType(_returnType).GetConstructor(new[] { _returnType });
                    }

                    if (methodInfo.ReturnType == typeof(Task))
                    {
                        var invoke = GetInvoker();

                        _invoker = async (handler, message, serviceProvider) =>
                        {
                            try
                            {
                                await (Task)invoke(handler, message, serviceProvider);
                            }
                            catch (Exception exc)
                            {
                                return new FailureDispatchResult(exc);
                            }

                            return new SuccessDispatchResult();
                        };
                    }
                    else if (typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
                    {
                        var invoke = GetInvoker();
                        var evaluator = GetAsyncEvaluation();

                        _invoker = async (handler, message, serviceProvider) =>
                        {
                            var task = (Task)invoke(handler, message, serviceProvider);

                            try
                            {
                                await task;
                            }
                            catch (Exception exc)
                            {
                                return new FailureDispatchResult(exc);
                            }

                            return evaluator(task);
                        };
                    }
                    else if (methodInfo.ReturnType == typeof(void))
                    {
                        var invoke = GetVoidInvoker();

                        _invoker = (handler, message, serviceProvider) =>
                        {
                            try
                            {
                                invoke(handler, message, serviceProvider);
                            }
                            catch (Exception exc)
                            {
                                return Task.FromResult<IDispatchResult>(new FailureDispatchResult(exc));
                            }

                            return Task.FromResult<IDispatchResult>(new SuccessDispatchResult());
                        };
                    }
                    else
                    {
                        var invoke = GetInvoker();
                        var evaluator = GetSyncEvaluation();

                        _invoker = (handler, message, serviceProvider) =>
                        {
                            object result;
                            try
                            {
                                result = invoke(handler, message, serviceProvider);
                            }
                            catch (Exception exc)
                            {
                                return Task.FromResult<IDispatchResult>(new FailureDispatchResult(exc));
                            }

                            if (result == null)
                            {
                                return Task.FromResult<IDispatchResult>(new FailureDispatchResult());
                            }

                            if (result is IDispatchResult dispatchResult)
                            {
                                return Task.FromResult(dispatchResult);
                            }

                            return Task.FromResult(evaluator(result));
                        };
                    }
                }

                private Func<object, object, IServiceProvider, object> GetInvoker()
                {
                    var messageType = _methodInfo.GetParameters().First().ParameterType;
                    var handlerParameter = Expression.Parameter(typeof(object), "handler");
                    var messageParameter = Expression.Parameter(typeof(object), "message");
                    var serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
                    var invocation = BuildInvocation(Expression.Convert(handlerParameter, _methodInfo.DeclaringType),
                                                     Expression.Convert(messageParameter, messageType),
                                                     serviceProviderParameter);

                    if (_methodInfo.ReturnType.IsValueType)
                    {
                        invocation = Expression.Convert(invocation, typeof(object));
                    }

                    var invoke = Expression.Lambda<Func<object, object, IServiceProvider, object>>(invocation,
                                                                                                   handlerParameter,
                                                                                                   messageParameter,
                                                                                                   serviceProviderParameter)
                                           .Compile();
                    return invoke;
                }

                private Action<object, object, IServiceProvider> GetVoidInvoker()
                {
                    var messageType = _methodInfo.GetParameters().First().ParameterType;
                    var handlerParameter = Expression.Parameter(typeof(object), "handler");
                    var messageParameter = Expression.Parameter(typeof(object), "message");
                    var serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
                    var invocation = BuildInvocation(Expression.Convert(handlerParameter, _methodInfo.DeclaringType),
                                                     Expression.Convert(messageParameter, messageType),
                                                     serviceProviderParameter);

                    var invoke = Expression.Lambda<Action<object, object, IServiceProvider>>(invocation,
                                                                                             handlerParameter,
                                                                                             messageParameter,
                                                                                             serviceProviderParameter)
                                           .Compile();
                    return invoke;
                }

                private Expression BuildInvocation(Expression handlerParameter,
                                                   Expression messageParameter,
                                                   Expression serviceProviderParameter)
                {
                    var parameters = _methodInfo.GetParameters();
                    var arguments = new Expression[parameters.Length];

                    arguments[0] = messageParameter;

                    for (var i = 1; i < arguments.Length; i++)
                    {
                        var parameter = parameters[i];
                        var parameterType = parameter.ParameterType;

                        // TODO: Implement custom service type lookup.
                        var injectAttribute = parameter.GetCustomAttribute<InjectAttribute>(inherit: false);

                        if (injectAttribute != null)
                        {
                            var serviceType = parameterType;

                            if (injectAttribute.ServiceType != null)
                            {
                                if (!parameterType.IsAssignableFrom(injectAttribute.ServiceType))
                                {
                                    throw new InvalidOperationException(); // TODO: Exception message
                                }

                                serviceType = injectAttribute.ServiceType;
                            }

                            Assert(_getServiceMethod != null);

                            var serviceTypeConstant = Expression.Constant(serviceType);
                            var getServiceCall = Expression.Call(_getServiceMethod, serviceProviderParameter, serviceTypeConstant);
                            var convertedService = Expression.Convert(getServiceCall, parameterType);

                            arguments[i] = convertedService;
                        }
                        else
                        {
                            arguments[i] = Expression.Default(parameterType);
                        }
                    }

                    return Expression.Call(handlerParameter, _methodInfo, arguments);
                }

                private Func<Task, IDispatchResult> GetAsyncEvaluation()
                {
                    Func<Task, IDispatchResult> compiledLambda;

                    var taskParameter = Expression.Parameter(typeof(Task), "task");
                    var taskParameterConversion = Expression.Convert(taskParameter, typeof(Task<>).MakeGenericType(_returnType));
                    var resultAccess = Expression.Property(taskParameterConversion, typeof(Task<>).MakeGenericType(_returnType).GetProperty("Result"));
                    var lambda = Expression.Lambda<Func<Task, IDispatchResult>>(Evaluate(resultAccess), taskParameter);
                    compiledLambda = lambda.Compile();
                    return compiledLambda;
                }

                private Func<object, IDispatchResult> GetSyncEvaluation()
                {
                    var resultParameter = Expression.Parameter(typeof(object), "result");
                    var convertedResult = Expression.Convert(resultParameter, _returnType);

                    var successResult = Expression.New(_createTypedSuccessDispatchResult, convertedResult);

                    return Expression.Lambda<Func<object, IDispatchResult>>(successResult, resultParameter).Compile();
                }

                private Expression Evaluate(Expression invocation)
                {
                    var returnTarget = Expression.Label(typeof(IDispatchResult));
                    var result = new List<Expression>();

                    Expression Return(Expression ret)
                    {
                        return Expression.Return(returnTarget, ret, typeof(IDispatchResult));
                    }

                    var variable = Expression.Variable(_returnType, "result");
                    result.Add(Expression.Assign(variable, invocation));
                    result.Add(Expression.IfThen(Expression.TypeIs(variable, typeof(IDispatchResult)), Return(Expression.Convert(variable, typeof(IDispatchResult)))));

                    var nullCondition = Expression.Equal(variable, Expression.Constant(null));
                    var failureResult = Expression.New(typeof(FailureDispatchResult));
                    result.Add(Expression.IfThen(nullCondition, Return(failureResult)));

                    var successResult = Expression.New(_createTypedSuccessDispatchResult, variable);
                    result.Add(Return(successResult));


                    var returnLabel = Expression.Label(returnTarget, Expression.Constant(null, typeof(IDispatchResult)));
                    result.Add(returnLabel);
                    return Expression.Block(new[] { variable }, result);
                }

                public Task<IDispatchResult> Invoke(object handler, object message, IServiceProvider serviceProvider)
                {
                    if (handler == null)
                        throw new ArgumentNullException(nameof(handler));

                    if (message == null)
                        throw new ArgumentNullException(nameof(message));

                    if (serviceProvider == null)
                        throw new ArgumentNullException(nameof(serviceProvider));

                    return _invoker(handler, message, serviceProvider);
                }
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

        private sealed class MessageHandlerProvider<TMessage> : IContextualProvider<IMessageHandler<TMessage>>
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

    public class MessageHandlerFeature
    {
        public IList<Type> MessageHandlers { get; } = new List<Type>();
    }

    public class MessageHandlerFeatureProvider : IApplicationFeatureProvider<MessageHandlerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, MessageHandlerFeature feature)
        {
            foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
            {
                foreach (var type in part.Types)
                {
                    if (IsMessageHandler(type) && !feature.MessageHandlers.Contains(type))
                    {
                        feature.MessageHandlers.Add(type);
                    }
                }
            }
        }

        protected virtual bool IsMessageHandler(Type type)
        {
            return (type.IsClass || type.IsValueType && !type.IsEnum) &&
                   !type.IsAbstract &&
                   type.IsPublic &&
                   !type.ContainsGenericParameters &&
                   !type.IsDefined<NoMessageHandlerAttribute>(inherit: false) &&
                   (type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) || type.IsDefined<MessageHandlerAttribute>(inherit: true));
        }
    }
}
