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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Serialization;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.Messaging
{
    public sealed class MessageDispatcher : IMessageDispatcher, IAsyncInitialization
    // We need to implement IAsyncInitialization in order to enable this type beeing registered as app-service.
    {
        #region Fields

        private readonly IMessageHandlerRegistry _messageHandlerRegistry;
        private readonly IMessageRouterFactory _messageRouterFactory;
        private readonly IMessageSerializer _serializer;
        private readonly ITypeResolver _typeResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<MessagingOptions> _optionsAccessor;
        private readonly ILogger<MessageDispatcher>? _logger;

        // Caching the delegates for performance reasons.
        private readonly Func<IDispatchResult, Message> _serializeDispatchResult;
        private readonly Func<DispatchDataDictionary, Message> _serializeDispatchData;

        private volatile IMessageHandlerProvider _messageHandlerProvider;

        private readonly AsyncInitializationHelper<IMessageRouter> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        #endregion

        #region C'tor

        public MessageDispatcher(
            IMessageHandlerRegistry messageHandlerRegistry,
            IMessageRouterFactory messageRouterFactory,
            IMessageSerializer serializer,
            ITypeResolver typeResolver,
            IServiceProvider serviceProvider,
            IOptions<MessagingOptions> optionsAccessor,
            ILogger<MessageDispatcher>? logger = null)
        {
            if (messageHandlerRegistry == null)
                throw new ArgumentNullException(nameof(messageHandlerRegistry));

            if (messageRouterFactory == null)
                throw new ArgumentNullException(nameof(messageRouterFactory));

            if (serializer is null)
                throw new ArgumentNullException(nameof(serializer));

            if (typeResolver is null)
                throw new ArgumentNullException(nameof(typeResolver));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _messageHandlerRegistry = messageHandlerRegistry;
            _messageRouterFactory = messageRouterFactory;
            _serializer = serializer;
            _typeResolver = typeResolver;
            _serviceProvider = serviceProvider;
            _optionsAccessor = optionsAccessor;
            _logger = logger;

            _serializeDispatchResult = _serializer.Serialize;
            _serializeDispatchData = _serializer.Serialize;

            _messageHandlerProvider = null!;
            ReloadMessageHandlers();

            _initializationHelper = new AsyncInitializationHelper<IMessageRouter>(InitializeAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        #region Disposal

        /// <inheritdoc />
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            var (success, messageRouter) = await _initializationHelper
                .CancelAsync()
                .ConfigureAwait(false);

            if (success)
            {
                await messageRouter.DisposeAsync();
            }
        }

        #endregion

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task<IMessageRouter> InitializeAsync(CancellationToken cancellation)
        {
            // Create the underlying message router.
            var messageRouter = await _messageRouterFactory.CreateMessageRouterAsync(
                _optionsAccessor.Value?.LocalEndPoint ?? MessagingOptions.DefaultLocalEndPoint,
                new RouteMessageHandler(this),
                cancellation);

            try
            {
                var routeRegistrations = BuildRouteRegistrations(MessageHandlerProvider);
                await messageRouter
                    .RegisterRoutesAsync(routeRegistrations, cancellation)
                    .ConfigureAwait(false);

                _logger?.LogDebug("Remote message dispatcher initialized.");
            }
            catch
            {
                await messageRouter.DisposeAsync();
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
            return messageHandlerProvider
                .GetHandlerRegistrations()
                .GroupBy(MessageHandlerRegistrationExtensions.GetRoute, BuildRouteRegistration);
        }

        private static RouteRegistration BuildRouteRegistration(Route route, IEnumerable<IMessageHandlerRegistration> handlerRegistrations)
        {
            return new RouteRegistration(route, GetRouteOptions(handlerRegistrations));
        }

        private static RouteRegistrationOptions GetRouteOptions(IEnumerable<IMessageHandlerRegistration> handlerRegistrations)
        {
            var result = RouteRegistrationOptions.Default;

            var firstOption = true;
            foreach (var handlerRegistration in handlerRegistrations)
            {
                var option = handlerRegistration.GetRouteOptions();
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

        #endregion

        private IList<IRouteResolver> RoutesResolver
            => _optionsAccessor.Value?.RoutesResolvers ?? Array.Empty<IRouteResolver>();

        public void ReloadMessageHandlers()
        {
            var messageHandlerProvider = _messageHandlerProvider; // Volatile read op.

            if (messageHandlerProvider == null)
            {
                messageHandlerProvider = _messageHandlerRegistry.Provider;
                var previous = Interlocked.CompareExchange(ref _messageHandlerProvider, messageHandlerProvider, null!);

                if (previous != null)
                {
                    messageHandlerProvider = previous;
                }
            }

            Debug.Assert(messageHandlerProvider != null);
        }

        public IMessageHandlerProvider MessageHandlerProvider => _messageHandlerProvider; // Volatile read op;

        public async ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            var router = await GetMessageRouterAsync(cancellation).ConfigureAwait(false);
            return await router.GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
        }

        private sealed class RouteMessageHandler : IRouteMessageHandler
        {
            private readonly MessageDispatcher _messageDispatcher;

            public RouteMessageHandler(MessageDispatcher messageDispatcher)
            {
                Debug.Assert(messageDispatcher != null);

                _messageDispatcher = messageDispatcher!;
            }

            public async ValueTask<RouteMessageHandleResult> HandleAsync(
                RouteMessage<DispatchDataDictionary> routeMessage,
                Route route,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                try
                {
                    return await HandleUnprotectedAsync(routeMessage, route, publish, localDispatch, cancellation);
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    var dispatchResult = _messageDispatcher.WrapException(exc);
                    var resultRouteMessage = _messageDispatcher.BuildRouteMessage(dispatchResult);
                    return new RouteMessageHandleResult(resultRouteMessage, handled: false);
                }
            }

            private async ValueTask<RouteMessageHandleResult> HandleUnprotectedAsync(
                RouteMessage<DispatchDataDictionary> routeMessage,
                Route route,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                if (!_messageDispatcher.TryGetDispatchData(routeMessage, out var dispatchData))
                {
                    var dispatchResult = new FailureDispatchResult("Unable to deserialize the dispatch data."); // TODO
                    var resultRouteMessage = _messageDispatcher.BuildRouteMessage(dispatchResult);
                    return new RouteMessageHandleResult(resultRouteMessage, handled: false);
                }
                else
                {
                    // The type of message in the dispatch-data is not necessarily the type that we use for dispatch,
                    // because of route descend. The route contains the actual message-type that we use for dispatch.
                    // The message-type that the dispatch-data provices MUST ALWAYS be assignable to the message-type
                    // in the route.

                    // This returns false, if it is the default value of the route-type,
                    // or the message-type encoded in the route could not be load.
                    if (!route.TryGetMessageType(_messageDispatcher._typeResolver, out var messageType))
                    {
                        // TODO: Is this an error, or can we safely fallback to the type encoded in the dispatch-data?
                        messageType = dispatchData.MessageType;
                    }

                    // TODO: Can we safely do this? This bypasses any reflection-context that we set up previously. 
                    //       If this is used to store the type somewhere and the reflection context is a
                    //       WeakReflectionContext, unload is impossible.
                    //       This is needed here, because we compare to runtime types (via equality or IsAssignableFrom)
                    //       in many cases in the messaging system. 
                    //       In the long term we should get rid of this.
                    messageType = messageType.UnderlyingSystemType;

                    Debug.Assert(messageType != null);
                    Debug.Assert(messageType!.IsAssignableFrom(dispatchData.MessageType));

                    // We allow target route descend on publishing only (See https://github.com/AI4E/AI4E/issues/82#issuecomment-448269275)
                    // TODO: Is this correct for dispatching to known end-point, too?
                    var (dispatchResult, handlersFound) = await _messageDispatcher.InternalDispatchLocalAsync(
                        messageType!,
                        dispatchData,
                        publish,
                        allowRouteDescend: publish,
                        localDispatch,
                        cancellation);

                    var resultRouteMessage = _messageDispatcher.BuildRouteMessage(dispatchResult);
                    return new RouteMessageHandleResult(resultRouteMessage, handled: handlersFound);
                }
            }
        }

        private RouteMessage<DispatchDataDictionary> BuildRouteMessage(DispatchDataDictionary dispatchData)
        {
            return new RouteMessage<DispatchDataDictionary>(dispatchData, _serializeDispatchData);
        }

        private RouteMessage<IDispatchResult> BuildRouteMessage(IDispatchResult dispatchResult)
        {
            return new RouteMessage<IDispatchResult>(dispatchResult, _serializeDispatchResult);
        }

        private bool IsFromSameContext(object obj)
        {
            var objType = obj.GetType();
            var objTypeName = objType.GetUnqualifiedTypeName();

            return _typeResolver.TryResolveType(objTypeName.AsSpan(), out var type) &&
                   type == objType;
        }

        private bool TryGetDispatchData(
            RouteMessage<DispatchDataDictionary> routeMessage,
            [NotNullWhen(true)] out DispatchDataDictionary? dispatchData)
        {
            if (routeMessage.TryGetOriginal(out dispatchData) && IsFromSameContext(dispatchData))
            {
                return true;
            }

            if (_serializer.TryDeserialize(routeMessage.Message, out dispatchData))
            {
                return true;
            }

            dispatchData = null;
            return false;
        }

        private IDispatchResult GetDispatchResult(RouteMessage<IDispatchResult> routeMessage)
        {
            if (routeMessage.TryGetOriginal(out var dispatchResult) && IsFromSameContext(dispatchResult))
            {
                return dispatchResult;
            }

            if (!_serializer.TryDeserialize(routeMessage.Message, out dispatchResult))
            {
                dispatchResult = new FailureDispatchResult("Unable to deserialize the dispatch result"); // TODO
            }

            return dispatchResult;
        }

        private IDispatchResult WrapException(Exception exc)
        {
            if (_optionsAccessor.Value.EnableVerboseFailureResults)
            {
                return new FailureDispatchResult(exc);
            }

            return new FailureDispatchResult("An error occured while handling the message.");
        }

        #region Dispatch

        public async ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            RouteEndPointAddress endPoint,
            CancellationToken cancellation = default)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            if (endPoint != default)
            {
                return await InternalDispatchAsync(dispatchData, publish, endPoint, cancellation);
            }

            return await InternalDispatchAsync(dispatchData, publish, cancellation);
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
            var messageRouter = await GetMessageRouterAsync(cancellation)
                .ConfigureAwait(false);

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

            // TODO: Does the route-descend work correctly, if we route the message like this?
            var route = new Route(dispatchData.MessageType);
            var routeMessage = BuildRouteMessage(dispatchData);
            var resultRouteMessage = await messageRouter.RouteAsync(route, routeMessage, publish, endPoint, cancellation);
            return GetDispatchResult(resultRouteMessage);
        }

        private async ValueTask<IDispatchResult> InternalDispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            var messageRouter = await GetMessageRouterAsync(cancellation)
                .ConfigureAwait(false);

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
            foreach (var routesResolver in RoutesResolver)
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

            await _initializationHelper.Initialization
                                       .WithCancellation(cancellation)
                                       .ConfigureAwait(false);

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
                    Debug.Assert(currType != null);

                    var handlerRegistrations = messageHandlerProvider.GetHandlerRegistrations(currType!);

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
                                return (result!, handlersFound: true);
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                }
                while (allowRouteDescend && !currType!.IsInterface && (currType = currType.BaseType!) != null);

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
                    return ((await tasks[0]).result!, handlersFound: true);
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
            Debug.Assert(dispatchData != null);
            Debug.Assert(handlerRegistrations != null);
            Debug.Assert(handlerRegistrations.Any());

            if (publish)
            {
                var dispatchOperations = new List<ValueTask<IDispatchResult>>(capacity: handlerRegistrations!.Count);

                foreach (var handlerRegistration in handlerRegistrations)
                {
                    if (!localDispatch && handlerRegistration.IsLocalDispatchOnly())
                    {
                        continue;
                    }

                    async ValueTask<IDispatchResult> DispatchProtected()
                    {
                        try
                        {
                            return await InternalDispatchLocalSingleHandlerAsync(
                                handlerRegistration, dispatchData!, publish, localDispatch, cancellation);
                        }
#pragma warning disable CA1031
                        catch (Exception exc)
#pragma warning restore CA1031
                        {
                            return WrapException(exc);
                        }
                    }

                    var dispatchOperation = DispatchProtected();

                    dispatchOperations.Add(dispatchOperation);
                }

                if (!dispatchOperations.Any())
                {
                    return (result: new SuccessDispatchResult(), handlersFound: false);
                }

                var dispatchResults = (await dispatchOperations.WhenAll(preserveOrder: false))
                    .Where(p => !(p is DispatchFailureDispatchResult))
                    .ToList();

                if (!dispatchResults.Any())
                {
                    return (result: new SuccessDispatchResult(), handlersFound: false);
                }

                if (dispatchResults.Count == 1)
                {
                    return (result: dispatchResults[0], handlersFound: true);
                }

                return (result: new AggregateDispatchResult(dispatchResults), handlersFound: true);
            }
            else
            {
                foreach (var handlerRegistration in handlerRegistrations!)
                {
                    if (handlerRegistration.IsPublishOnly())
                    {
                        continue;
                    }

                    if (!localDispatch && handlerRegistration.IsLocalDispatchOnly())
                    {
                        continue;
                    }

                    IDispatchResult result;

                    try
                    {
                        result = await InternalDispatchLocalSingleHandlerAsync(
                            handlerRegistration, dispatchData!, publish, localDispatch, cancellation);

                        // If the handler returns with a dispatch failure, this is the same as if we never had found 
                        // the handler to enable dynamically opting out of message handling.
                        // This is not an exceptional case, in the sense of returning a failure here.
                        if (result.IsDispatchFailure())
                        {
                            continue;
                        }
                    }
#pragma warning disable CA1031
                    catch (Exception exc)
#pragma warning restore CA1031
                    {
                        result = WrapException(exc);
                    }

                    return (result, handlersFound: true);
                }

                return (result: new DispatchFailureDispatchResult(dispatchData!.MessageType), handlersFound: false);
            }
        }

        private async ValueTask<IDispatchResult> InternalDispatchLocalSingleHandlerAsync(
            IMessageHandlerRegistration handlerRegistration,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Debug.Assert(handlerRegistration != null);
            Debug.Assert(dispatchData != null);

            using var scope = _serviceProvider.CreateScope();
            var handler = handlerRegistration!.CreateMessageHandler(scope.ServiceProvider);

            if (handler == null)
            {
                // TODO: Log failure
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData!.MessageType}' to a handler that is null.");
            }

            if (!handler.MessageType.IsAssignableFrom(dispatchData!.MessageType))
            {
                // TODO: Log failure
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{handler.MessageType}'.");
            }

            return await handler.HandleAsync(dispatchData, publish, localDispatch, cancellation);
        }

        #endregion
    }
}
