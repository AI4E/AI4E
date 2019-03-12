/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RemoteMessageDispatcher.cs 
 * Types:           (1) AI4E.Routing.RemoteMessageDispatcher
 *                  (2) AI4E.Routing.RemoteMessageDispatcher.SerializationBinder
 *                  (3) AI4E.Routing.RemoteMessageDispatcher.SerializedMessageHandler
 * Version:         1.0
 * Author:          Andreas Trütschel
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Remoting;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class RemoteMessageDispatcher : IRemoteMessageDispatcher, IAsyncInitialization, IDisposable
    {
        #region Fields

        private readonly IMessageRouterFactory _messageRouterFactory;
        private readonly ITypeConversion _typeConversion;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteMessageDispatcher> _logger;

        private readonly MessageDispatcher _localMessageDispatcher;
        private readonly IMessageRouter _messageRouter;

        private readonly AsyncLifetimeManager _lifetimeManager;

        #endregion

        #region C'tor

        public RemoteMessageDispatcher(IMessageHandlerRegistry messageHandlerRegistry,
                                       IMessageRouterFactory messageRouterFactory,
                                       ITypeConversion typeConversion,
                                       IServiceProvider serviceProvider,
                                       ILogger<RemoteMessageDispatcher> logger)
        {
            if (messageHandlerRegistry == null)
                throw new ArgumentNullException(nameof(messageHandlerRegistry));

            if (messageRouterFactory == null)
                throw new ArgumentNullException(nameof(messageRouterFactory));

            if (typeConversion == null)
                throw new ArgumentNullException(nameof(typeConversion));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _messageRouterFactory = messageRouterFactory;
            _typeConversion = typeConversion;
            _serviceProvider = serviceProvider;
            _logger = logger;

            _localMessageDispatcher = new MessageDispatcher(messageHandlerRegistry, serviceProvider);
            _messageRouter = _messageRouterFactory.CreateMessageRouter(new SerializedMessageHandler(this));

            _lifetimeManager = BuildLifetimeManager();
        }

        #endregion

        private AsyncLifetimeManager BuildLifetimeManager()
        {
            async Task InitializeAsync(CancellationToken cancellation)
            {
                var messageHandlerProvider = _localMessageDispatcher.MessageHandlerProvider;
                var routeRegistrations = GetRouteRegistrations(messageHandlerProvider);
                var registrationOperations = routeRegistrations.Select(p => _messageRouter.RegisterRouteAsync(p, cancellation: default));
                await Task.WhenAll(registrationOperations);
                _logger?.LogDebug("Remote message dispatcher initialized.");
            }

            async Task DisposeAsync()
            {
                try
                {
                    await _messageRouter.UnregisterRoutesAsync(removePersistentRoutes: false);
                }
                finally
                {
                    _messageRouter.Dispose();
                }
            }

            return new AsyncLifetimeManager(InitializeAsync, DisposeAsync);
        }

        private static IEnumerable<RouteRegistration> GetRouteRegistrations(IMessageHandlerProvider messageHandlerProvider)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            Route GetRoute(IMessageHandlerRegistration handlerRegistration)
            {
                return new Route(handlerRegistration.MessageType.GetUnqualifiedTypeName());
            }

            RouteRegistrationOptions GetRouteOptions(IMessageHandlerRegistration handlerRegistration)
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

            RouteRegistrationOptions CombineRouteOptions(IEnumerable<RouteRegistrationOptions> options)
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

        public Task Initialization => _lifetimeManager.Initialization;

        #region Disposal

        public void Dispose()
        {
            _lifetimeManager.Dispose();
        }

        #endregion

        #region Serializer

        private readonly ThreadLocal<JsonSerializer> _serializer = new ThreadLocal<JsonSerializer>(BuildSerializer, trackAllValues: false);

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

        private JsonSerializer Serializer => _serializer.Value;

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

        public ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return _messageRouterFactory.GetDefaultEndPointAsync(cancellation);
        }

        private sealed class SerializedMessageHandler : ISerializedMessageHandler
        {
            private readonly RemoteMessageDispatcher _remoteMessageDispatcher;
            private readonly ITypeConversion _typeConversion;

            public SerializedMessageHandler(RemoteMessageDispatcher remoteMessageDispatcher)
            {
                Assert(remoteMessageDispatcher != null);

                _remoteMessageDispatcher = remoteMessageDispatcher;
                _typeConversion = _remoteMessageDispatcher._typeConversion;
            }

            public async ValueTask<(IMessage response, bool handled)> HandleAsync(
                Route route,
                IMessage request,
                bool publish,
                 bool localDispatch,
                CancellationToken cancellation)
            {
                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                var messageType = _typeConversion.DeserializeType(route.ToString());

                Assert(messageType != null);

                var dispatchData = _remoteMessageDispatcher.DeserializeDispatchData(request);

                // We allow target route descend on publishing only (See https://github.com/AI4E/AI4E/issues/82#issuecomment-448269275)
                var (dispatchResult, handlersFound) = await _remoteMessageDispatcher.TryDispatchLocalAsync(
                    dispatchData,
                    publish,
                    allowRouteDescend: publish,
                    localDispatch,
                    cancellation);

                var response = new Message();
                _remoteMessageDispatcher.SerializeDispatchResult(response, dispatchResult);
                request.PushFrame();

                return (response, handled: handlersFound);
            }
        }

        private void SerializeDispatchResult(IMessage message, IDispatchResult dispatchResult)
        {
            Assert(message != null);
            Assert(dispatchResult != null);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new StreamWriter(stream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                Serializer.Serialize(jsonWriter, dispatchResult, typeof(IDispatchResult));
            }
        }

        private IDispatchResult DeserializeDispatchResult(IMessage message)
        {
            Assert(message != null);

            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return Serializer.Deserialize<IDispatchResult>(jsonReader);
            }
        }

        private void SerializeDispatchData(IMessage message, DispatchDataDictionary dispatchData)
        {
            Assert(message != null);
            Assert(dispatchData != null);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var stringBuffer = new StringBuilder();

                using (var loggerWriter = new StringWriter(stringBuffer))
                using (var loggerJsonWriter = new JsonTextWriter(loggerWriter))
                {
                    Serializer.Serialize(loggerJsonWriter, dispatchData, typeof(DispatchDataDictionary));
                }

                _logger.LogTrace($"Sending message: {stringBuffer.ToString()}");
            }

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new StreamWriter(stream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                Serializer.Serialize(jsonWriter, dispatchData, typeof(DispatchDataDictionary));
            }
        }

        private DispatchDataDictionary DeserializeDispatchData(IMessage message)
        {
            Assert(message != null);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var frameIdx = message.FrameIndex;

                using (var stream = message.PopFrame().OpenStream())
                using (var reader = new StreamReader(stream))
                {
                    _logger.LogTrace($"Received message: {reader.ReadToEnd()}");
                }

                message.PushFrame();
                Assert(message.FrameIndex == frameIdx);
            }

            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return Serializer.Deserialize<DispatchDataDictionary>(jsonReader);
            }
        }

        #region Dispatch

        public ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            EndPointAddress endPoint,
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
            EndPointAddress endPoint,
            CancellationToken cancellation)
        {
            await Initialization.WithCancellation(cancellation);

            if (endPoint == await GetLocalEndPointAsync(cancellation))
            {
                var (result, _) = await TryDispatchLocalAsync(dispatchData, publish, allowRouteDescend: true, localDispatch: true, cancellation);
                return result;
            }
            else
            {
                var route = new Route(_typeConversion.SerializeType(dispatchData.MessageType));
                var serializedMessage = new Message();

                SerializeDispatchData(serializedMessage, dispatchData);

                var serializedResult = await _messageRouter.RouteAsync(route, serializedMessage, publish, endPoint, cancellation);
                var result = DeserializeDispatchResult(serializedResult);

                return result;
            }
        }

        private async ValueTask<IDispatchResult> InternalDispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            await Initialization.WithCancellation(cancellation);

            var routes = GetRoutes(dispatchData.MessageType);
            var serializedMessage = new Message();
            SerializeDispatchData(serializedMessage, dispatchData);

            var serializedResults = await _messageRouter.RouteAsync(routes, serializedMessage, publish, cancellation);
            var results = serializedResults.Select(p => DeserializeDispatchResult(p)).ToList();

            if (results.Count == 0)
            {
                if (publish)
                {
                    return new SuccessDispatchResult();
                }

                return new DispatchFailureDispatchResult(dispatchData.MessageType);
            }

            if (results.Count == 1)
            {
                var result = results.First();

                return result;
            }

            return new AggregateDispatchResult(results);
        }

        private RouteHierarchy GetRoutes(Type messageType)
        {
            if (messageType.IsInterface)
            {
                var route = new Route(_typeConversion.SerializeType(messageType));

                return new RouteHierarchy(ImmutableArray.Create(route));
            }

            var result = ImmutableArray.CreateBuilder<Route>();

            for (; messageType != null; messageType = messageType.BaseType)
            {
                result.Add(new Route(_typeConversion.SerializeType(messageType)));
            }

            return new RouteHierarchy(result.ToImmutable());
        }

        private async ValueTask<(IDispatchResult result, bool handlersFound)> TryDispatchLocalAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool allowRouteDescend,
            bool localDispatch,
            CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            _logger?.LogInformation($"End-point '{localEndPoint}': Dispatching message of type {dispatchData.MessageType} locally.");

            try
            {
                return await _localMessageDispatcher.TryDispatchAsync(dispatchData, publish, localDispatch, allowRouteDescend, cancellation);
            }
            finally
            {
                _logger?.LogDebug($"End-point '{localEndPoint}': Dispatched message of type {dispatchData.MessageType} locally.");
            }
        }

        public async ValueTask<IDispatchResult> DispatchLocalAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            await Initialization.WithCancellation(cancellation);

            var (dispatchResult, _) = await TryDispatchLocalAsync(dispatchData, publish, allowRouteDescend: true, localDispatch: true, cancellation);
            return dispatchResult;
        }

        #endregion
    }
}
