/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Messaging.Routing;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Messaging.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static System.Diagnostics.Debug;

namespace AI4E.Messaging
{
    public sealed class MessageDispatcher
        : IMessageDispatcher, IAsyncInitialization
    // We need to implement IAsyncInitialization in order to enable this type beeing registered as app-service.
    {
        // Caching the delegates for performance reasons.
        private static readonly Func<IDispatchResult, Message> _serializeDispatchResult = SerializeDispatchResult;
        private static readonly Func<DispatchDataDictionary, Message> _serializeDispatchData = SerializeDispatchData;

        #region Fields

        private readonly IMessageHandlerRegistry _messageHandlerRegistry;
        private readonly IServiceProvider _serviceProvider;
        private volatile IMessageHandlerProvider _messageHandlerProvider;

        private readonly IMessageRouterFactory _messageRouterFactory;
        private readonly ILogger<MessageDispatcher> _logger;
        private readonly IList<IRouteResolver> _routesResolver;
        private readonly AsyncInitializationHelper<IMessageRouter> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        #endregion

        #region C'tor

        public MessageDispatcher(
            IMessageHandlerRegistry messageHandlerRegistry,
            IMessageRouterFactory messageRouterFactory,
            IServiceProvider serviceProvider,
            IOptions<MessagingOptions> optionsAccessor,
            ILogger<MessageDispatcher> logger = null)
        {
            if (messageHandlerRegistry == null)
                throw new ArgumentNullException(nameof(messageHandlerRegistry));

            if (messageRouterFactory == null)
                throw new ArgumentNullException(nameof(messageRouterFactory));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _messageRouterFactory = messageRouterFactory;
            _logger = logger;
            _routesResolver = optionsAccessor.Value?.RoutesResolvers ?? Array.Empty<IRouteResolver>();

            _messageHandlerRegistry = messageHandlerRegistry;
            _serviceProvider = serviceProvider;

            ReloadMessageHandlers();

            _initializationHelper = new AsyncInitializationHelper<IMessageRouter>(InitializeAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        private async Task DisposeInternalAsync()
        {
            var (success, messageRouter) = await _initializationHelper.CancelAsync();

            if (success)
            {
                try
                {
                    await messageRouter.UnregisterRoutesAsync(removePersistentRoutes: false);
                }
                finally
                {
                    messageRouter.Dispose();
                }
            }
        }

        #endregion

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task<IMessageRouter> InitializeAsync(CancellationToken cancellation)
        {
            // Create the underlying message router.
            var messageRouter = await _messageRouterFactory.CreateMessageRouterAsync(
                new RouteMessageHandler(this), cancellation);

            try
            {
                var routeRegistrations = BuildRouteRegistrations(MessageHandlerProvider);
                await messageRouter.RegisterRoutesAsync(routeRegistrations, cancellation);
                _logger?.LogDebug("Remote message dispatcher initialized.");
            }
            catch
            {
                messageRouter.Dispose();
                throw;
            }

            return messageRouter;
        }

        private Task<IMessageRouter> GetMessageRouterAsync(CancellationToken cancellation)
        {
            return _initializationHelper.Initialization.WithCancellation(cancellation);
        }

        private static IEnumerable<RouteRegistration> BuildRouteRegistrations(IMessageHandlerProvider messageHandlerProvider)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            static Route GetRoute(IMessageHandlerRegistration handlerRegistration)
            {
                return new Route(handlerRegistration.MessageType.GetUnqualifiedTypeName());
            }

            static RouteRegistrationOptions GetRouteOptions(IMessageHandlerRegistration handlerRegistration)
            {
                var result = RouteRegistrationOptions.Default;

                if (handlerRegistration.IsPublishOnly())
                {
                    result |= RouteRegistrationOptions.PublishOnly;
                }

                if (handlerRegistration.IsTransient())
                {
                    result |= RouteRegistrationOptions.Transient;
                }

                if (handlerRegistration.IsLocalDispatchOnly())
                {
                    result |= RouteRegistrationOptions.LocalDispatchOnly;
                }

                return result;
            }

            static RouteRegistrationOptions CombineRouteOptions(IEnumerable<RouteRegistrationOptions> options)
            {
                var result = RouteRegistrationOptions.Default;

                var firstOption = true;
                foreach (var option in options)
                {
                    if (firstOption)
                    {
                        result = option;
                    }
                    else
                    {
                        result &= option;
                    }

                    firstOption = false;
                }

                return result;
            }

            return messageHandlerProvider
                .GetHandlerRegistrations()
                .GroupBy(GetRoute, (route, handlerRegistrations) => new RouteRegistration(route, CombineRouteOptions(handlerRegistrations.Select(GetRouteOptions))));
        }

        #endregion

        #region Serializer

        private static readonly ThreadLocal<JsonSerializer> _serializer = new ThreadLocal<JsonSerializer>(BuildSerializer, trackAllValues: false);

        private static JsonSerializer BuildSerializer()
        {
            var result = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new SerializationBinder()
            };

            result.Converters.Add(new TypeConverter());

            return result;
        }

        private static JsonSerializer Serializer => _serializer.Value;

        private sealed class SerializationBinder : ISerializationBinder
        {
            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                typeName = serializedType.GetUnqualifiedTypeName();
                assemblyName = null;
            }

            public Type BindToType(string assemblyName, string typeName)
            {
                return TypeLoadHelper.LoadTypeFromUnqualifiedName(typeName);
            }
        }

        #endregion

        public void ReloadMessageHandlers()
        {
            var messageHandlerProvider = _messageHandlerProvider; // Volatile read op.

            if (messageHandlerProvider == null)
            {
                messageHandlerProvider = _messageHandlerRegistry.ToProvider();
                var previous = Interlocked.CompareExchange(ref _messageHandlerProvider, messageHandlerProvider, null);

                if (previous != null)
                {
                    messageHandlerProvider = previous;
                }
            }

            Assert(messageHandlerProvider != null);
        }

        public IMessageHandlerProvider MessageHandlerProvider => _messageHandlerProvider; // Volatile read op;

        public ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return _messageRouterFactory.GetDefaultEndPointAsync(cancellation);
        }

        private sealed class RouteMessageHandler : IRouteMessageHandler
        {
            private readonly MessageDispatcher _remoteMessageDispatcher;

            public RouteMessageHandler(MessageDispatcher remoteMessageDispatcher)
            {
                Assert(remoteMessageDispatcher != null);

                _remoteMessageDispatcher = remoteMessageDispatcher;
            }

            public async ValueTask<RouteMessageHandleResult> HandleAsync(
                RouteMessage<DispatchDataDictionary> routeMessage,
                Route route,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                // TODO: This will be the underlying type for validate dispatches
                //       and will cause InternalDispatchLocalAsync to throw.
                //       We allow route-resolvers to customly resolve types to routes but
                //       tranlate back routes to types the default way always.
                //       A possible solution would be including the message type in the route itself.
                var messageType = TypeLoadHelper.LoadTypeFromUnqualifiedName(route.ToString());

                Assert(messageType != null);
                var dispatchData = GetDispatchData(routeMessage);

                // We allow target route descend on publishing only (See https://github.com/AI4E/AI4E/issues/82#issuecomment-448269275)
                var (dispatchResult, handlersFound) = await _remoteMessageDispatcher.InternalDispatchLocalAsync(
                    messageType,
                    dispatchData,
                    publish,
                    allowRouteDescend: publish,
                    localDispatch,
                    cancellation);

                var resultRouteMessage = BuildRouteMessage(dispatchResult);

                return new RouteMessageHandleResult(resultRouteMessage, handled: handlersFound);
            }
        }

        private static Message SerializeDispatchResult(IDispatchResult dispatchResult)
        {
            var messageBuilder = new MessageBuilder();

            Assert(dispatchResult != null);

            using var frameStream = messageBuilder.PushFrame().OpenStream();
            using var writer = new StreamWriter(frameStream);
            using var jsonWriter = new JsonTextWriter(writer);
            Serializer.Serialize(jsonWriter, dispatchResult, typeof(IDispatchResult));

            return messageBuilder.BuildMessage();
        }

        private static IDispatchResult DeserializeDispatchResult(Message message)
        {
            message.PopFrame(out var frame);

            using var frameStream = frame.OpenStream();
            using var reader = new StreamReader(frameStream);
            using var jsonReader = new JsonTextReader(reader);
            return Serializer.Deserialize<IDispatchResult>(jsonReader);
        }

        private static Message SerializeDispatchData(DispatchDataDictionary dispatchData)
        {
            var messageBuilder = new MessageBuilder();

            Assert(dispatchData != null);

            using var frameStream = messageBuilder.PushFrame().OpenStream();
            using var writer = new StreamWriter(frameStream);
            using var jsonWriter = new JsonTextWriter(writer);
            Serializer.Serialize(jsonWriter, dispatchData, typeof(DispatchDataDictionary));

            return messageBuilder.BuildMessage();
        }

        private static DispatchDataDictionary DeserializeDispatchData(Message message)
        {
            message.PopFrame(out var frame);

            using var frameStream = frame.OpenStream();
            using var reader = new StreamReader(frameStream);
            using var jsonReader = new JsonTextReader(reader);
            return Serializer.Deserialize<DispatchDataDictionary>(jsonReader);
        }

        private static RouteMessage<DispatchDataDictionary> BuildRouteMessage(DispatchDataDictionary dispatchData)
        {
            return new RouteMessage<DispatchDataDictionary>(dispatchData, _serializeDispatchData);
        }

        private static RouteMessage<IDispatchResult> BuildRouteMessage(IDispatchResult dispatchResult)
        {
            return new RouteMessage<IDispatchResult>(dispatchResult, _serializeDispatchResult);
        }

        private static DispatchDataDictionary GetDispatchData(RouteMessage<DispatchDataDictionary> routeMessage)
        {
            if (!routeMessage.TryGetOriginalMessage(out var dispatchData))
            {
                dispatchData = DeserializeDispatchData(routeMessage.Message);
            }

            return dispatchData;
        }

        private static IDispatchResult GetDispatchResult(RouteMessage<IDispatchResult> routeMessage)
        {
            if (!routeMessage.TryGetOriginalMessage(out var dispatchResult))
            {
                var message = routeMessage.Message;
                dispatchResult = DeserializeDispatchResult(message);
            }

            return dispatchResult;
        }

        #region Dispatch

        public ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            RouteEndPointAddress endPoint,
            CancellationToken cancellation = default)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            if (endPoint != default)
            {
                return InternalDispatchAsync(dispatchData, publish, endPoint, cancellation);
            }

            return InternalDispatchAsync(dispatchData, publish, cancellation);
        }

        public ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation = default)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            return InternalDispatchAsync(dispatchData, publish, cancellation);
        }

        private async ValueTask<IDispatchResult> InternalDispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            var messageRouter = await GetMessageRouterAsync(cancellation);

            if (endPoint == await GetLocalEndPointAsync(cancellation))
            {
                var (result, _) = await InternalDispatchLocalAsync(
                    dispatchData.MessageType,
                    dispatchData,
                    publish,
                    allowRouteDescend: true,
                    localDispatch: true,
                    cancellation);
                return result;
            }

            var route = new Route(dispatchData.MessageType.GetUnqualifiedTypeName());
            var routeMessage = BuildRouteMessage(dispatchData);
            var resultRouteMessage = await messageRouter.RouteAsync(route, routeMessage, publish, endPoint, cancellation);
            return GetDispatchResult(resultRouteMessage);
        }

        private async ValueTask<IDispatchResult> InternalDispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            var messageRouter = await GetMessageRouterAsync(cancellation);
            var routes = ResolveRoutes(dispatchData);
            var routeMessage = BuildRouteMessage(dispatchData);
            var resultRouteMessages = await messageRouter.RouteAsync(routes, routeMessage, publish, cancellation);

            if (resultRouteMessages.Count == 0)
            {
                if (publish)
                {
                    return new SuccessDispatchResult();
                }

                return new DispatchFailureDispatchResult(dispatchData.MessageType);
            }

            if (resultRouteMessages.Count == 1)
            {
                return GetDispatchResult(resultRouteMessages.First());
            }

            return new AggregateDispatchResult(resultRouteMessages.Select(GetDispatchResult));
        }

        private RouteHierarchy ResolveRoutes(DispatchDataDictionary dispatchData)
        {
            foreach (var routesResolver in _routesResolver)
            {
                if (routesResolver.TryResolve(dispatchData, out var routes))
                    return routes;
            }

            return RouteResolver.ResolveDefaults(dispatchData);
        }

        #endregion

        #region DispatchLocal

        public async ValueTask<IDispatchResult> DispatchLocalAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            await _initializationHelper.Initialization.WithCancellation(cancellation);

            var (dispatchResult, _) = await InternalDispatchLocalAsync(
                dispatchData.MessageType,
                dispatchData,
                publish,
                allowRouteDescend: true,
                localDispatch: true,
                cancellation);

            return dispatchResult;
        }

        private async ValueTask<(IDispatchResult result, bool handlersFound)> InternalDispatchLocalAsync(
            Type messageType,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool allowRouteDescend,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Debug.Assert(messageType.IsAssignableFrom(dispatchData.MessageType));

            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            _logger?.LogInformation($"End-point '{localEndPoint}': Dispatching message of type {dispatchData.MessageType} locally.");

            try
            {
                var messageHandlerProvider = MessageHandlerProvider;

                var currType = messageType;
                var tasks = new List<ValueTask<(IDispatchResult result, bool handlersFound)>>();

                do
                {
                    Assert(currType != null);

                    var handlerRegistrations = messageHandlerProvider.GetHandlerRegistrations(currType);

                    if (handlerRegistrations.Any())
                    {
                        var dispatchOperation = InternalDispatchLocalAsync(handlerRegistrations, dispatchData, publish, localDispatch, cancellation);

                        if (publish)
                        {
                            tasks.Add(dispatchOperation);
                        }
                        else
                        {
                            var (result, handlersFound) = await dispatchOperation;

                            if (handlersFound)
                            {
                                return (result, handlersFound: true);
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                }
                while (allowRouteDescend && !currType.IsInterface && (currType = currType.BaseType) != null);

                // When dispatching a message and no handlers are available, this is a failure.
                if (!publish)
                {
                    return (new DispatchFailureDispatchResult(dispatchData.MessageType), handlersFound: false);
                }

                var filteredResult = (await tasks.WhenAll(preserveOrder: false))
                    .Where(p => p.handlersFound)
                    .Select(p => p.result)
                    .ToList();

                // When publishing a message and no handlers are available, this is a success.
                if (filteredResult.Count == 0)
                {
                    return (new SuccessDispatchResult(), handlersFound: false);
                }

                if (filteredResult.Count == 1)
                {
                    return ((await tasks[0]).result, handlersFound: true);
                }

                return (new AggregateDispatchResult(filteredResult), handlersFound: true);
            }
            finally
            {
                _logger?.LogDebug($"End-point '{localEndPoint}': Dispatched message of type {dispatchData.MessageType} locally.");
            }
        }

        private async ValueTask<(IDispatchResult result, bool handlersFound)> InternalDispatchLocalAsync(
            IReadOnlyCollection<IMessageHandlerRegistration> handlerRegistrations,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Assert(dispatchData != null);
            Assert(handlerRegistrations != null);
            Assert(handlerRegistrations.Any());

            if (publish)
            {
                var dispatchOperations = new List<ValueTask<IDispatchResult>>(capacity: handlerRegistrations.Count);

                foreach (var handlerRegistration in handlerRegistrations)
                {
                    if (!localDispatch && handlerRegistration.IsLocalDispatchOnly())
                    {
                        continue;
                    }

                    var dispatchOperation = InternalDispatchLocalSingleHandlerAsync(
                        handlerRegistration, dispatchData, publish, localDispatch, cancellation);

                    dispatchOperations.Add(dispatchOperation);
                }

                if (!dispatchOperations.Any())
                {
                    return (result: new SuccessDispatchResult(), handlersFound: false);
                }

                var dispatchResults = await dispatchOperations.WhenAll(preserveOrder: false);

                if (dispatchResults.Count() == 1)
                {
                    return (result: dispatchResults.First(), handlersFound: true);
                }

                return (result: new AggregateDispatchResult(dispatchResults), handlersFound: true);
            }
            else
            {
                foreach (var handlerRegistration in handlerRegistrations)
                {
                    if (handlerRegistration.IsPublishOnly())
                    {
                        continue;
                    }

                    if (!localDispatch && handlerRegistration.IsLocalDispatchOnly())
                    {
                        continue;
                    }

                    var result = await InternalDispatchLocalSingleHandlerAsync(
                        handlerRegistration, dispatchData, publish, localDispatch, cancellation);

                    if (result.IsDispatchFailure())
                    {
                        continue;
                    }

                    return (result, handlersFound: true);
                }

                return (result: default, handlersFound: false);
            }
        }

        private async ValueTask<IDispatchResult> InternalDispatchLocalSingleHandlerAsync(
            IMessageHandlerRegistration handlerRegistration,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Assert(handlerRegistration != null);
            Assert(dispatchData != null);

            using var scope = _serviceProvider.CreateScope();
            var handler = handlerRegistration.CreateMessageHandler(scope.ServiceProvider);

            if (handler == null)
            {
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that is null.");
            }

            if (!handler.MessageType.IsAssignableFrom(dispatchData.MessageType))
            {
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{handler.MessageType}'.");
            }

            try
            {
                return await handler.HandleAsync(dispatchData, publish, localDispatch, cancellation);
            }
            catch (Exception exc)
            {
                return new FailureDispatchResult(exc);
            }
        }

        #endregion
    }
}
