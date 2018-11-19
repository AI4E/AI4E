/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RemoteMessageDispatcher.cs 
 * Types:           (1) AI4E.Routing.RemoteMessageDispatcher
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class RemoteMessageDispatcher : IRemoteMessageDispatcher
    {
        #region Fields

        private readonly IMessageRouter _messageRouter;
        private readonly ITypeConversion _typeConversion;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteMessageDispatcher> _logger;

        private readonly JsonSerializer _serializer;
        private readonly IMessageDispatcher _localMessageDispatcher;
        private readonly ConcurrentDictionary<Type, TypedMessageDisaptcher> _typedDispatchers = new ConcurrentDictionary<Type, TypedMessageDisaptcher>();

        #endregion

        #region C'tor

        public RemoteMessageDispatcher(IMessageRouterFactory messageRouterFactory,
                                       ITypeConversion typeConversion,
                                       IServiceProvider serviceProvider,
                                       ILogger<RemoteMessageDispatcher> logger)
        {
            if (messageRouterFactory == null)
                throw new ArgumentNullException(nameof(messageRouterFactory));

            if (typeConversion == null)
                throw new ArgumentNullException(nameof(typeConversion));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _typeConversion = typeConversion;
            _serviceProvider = serviceProvider;
            _logger = logger;

            _serializer = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new SerializationBinder()
            };

            _messageRouter = messageRouterFactory.CreateMessageRouter(new SerializedMessageHandler(this));

            _localMessageDispatcher = new MessageDispatcher(serviceProvider);
        }

        #endregion

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

        public ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return _messageRouter.GetLocalEndPointAsync(cancellation);
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

            public async ValueTask<IMessage> HandleAsync(string route,
                                                         IMessage serializedMessage,
                                                         bool publish,
                                                         CancellationToken cancellation)
            {
                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                if (serializedMessage == null)
                    throw new ArgumentNullException(nameof(serializedMessage));

                var messageType = _typeConversion.DeserializeType(route);

                Assert(messageType != null);

                var dispatchData = _remoteMessageDispatcher.DeserializeDispatchData(serializedMessage);
                var dispatchResult = await _remoteMessageDispatcher.DispatchLocalAsync(dispatchData, publish, cancellation);

                var response = new Message();
                _remoteMessageDispatcher.SerializeDispatchResult(response, dispatchResult);
                serializedMessage.PushFrame();

                return response;
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
                _serializer.Serialize(jsonWriter, dispatchResult, typeof(IDispatchResult));
            }
        }

        private IDispatchResult DeserializeDispatchResult(IMessage message)
        {
            Assert(message != null);

            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return _serializer.Deserialize<IDispatchResult>(jsonReader);
            }
        }

        private void SerializeDispatchData(IMessage message, DispatchDataDictionary dispatchData)
        {
            Assert(message != null);
            Assert(dispatchData != null);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new StreamWriter(stream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                _serializer.Serialize(jsonWriter, dispatchData, typeof(DispatchDataDictionary));
            }
        }

        private DispatchDataDictionary DeserializeDispatchData(IMessage message)
        {
            Assert(message != null);

            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return _serializer.Deserialize<DispatchDataDictionary>(jsonReader);
            }
        }

        #region Dispatch

        public Task<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData,
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

        public Task<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData,
                                                   bool publish,
                                                   CancellationToken cancellation = default)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            return InternalDispatchAsync(dispatchData, publish, cancellation);
        }

        private async Task<IDispatchResult> InternalDispatchAsync(DispatchDataDictionary dispatchData,
                                                                  bool publish,
                                                                  EndPointAddress endPoint,
                                                                  CancellationToken cancellation)
        {
            var route = _typeConversion.SerializeType(dispatchData.MessageType);
            var serializedMessage = new Message();

            SerializeDispatchData(serializedMessage, dispatchData);

            var serializedResult = await _messageRouter.RouteAsync(route, serializedMessage, publish, endPoint, cancellation);
            var result = DeserializeDispatchResult(serializedResult);

            return result;
        }

        private async Task<IDispatchResult> InternalDispatchAsync(DispatchDataDictionary dispatchData,
                                                                  bool publish,
                                                                  CancellationToken cancellation)
        {
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

        private IEnumerable<string> GetRoutes(Type messageType)
        {
            if (messageType.IsInterface)
            {
                return _typeConversion.SerializeType(messageType).Yield();
            }

            var result = new List<string>();

            for (; messageType != null; messageType = messageType.BaseType)
            {
                result.Add(_typeConversion.SerializeType(messageType));
            }

            return result;
        }



        public async Task<IDispatchResult> DispatchLocalAsync(DispatchDataDictionary dispatchData,
                                                              bool publish,
                                                              CancellationToken cancellation)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            _logger?.LogInformation($"End-point '{localEndPoint}': Dispatching message of type {dispatchData.MessageType} locally.");

            try
            {
                List<Task> combinedPendingRegistrations = null;

                var currType = dispatchData.MessageType;
                do
                {
                    var typedDispatcher = GetTypedDispatcher(currType);
                    var pendingRegistrations = typedDispatcher.PendingRegistrations;

                    if (!pendingRegistrations.Any())
                    {
                        continue;
                    }

                    if (combinedPendingRegistrations == null)
                        combinedPendingRegistrations = new List<Task>();

                    combinedPendingRegistrations.AddRange(pendingRegistrations);
                }
                while (!currType.IsInterface && (currType = currType.BaseType) != null);

                if (combinedPendingRegistrations != null)
                {
                    await Task.WhenAll(combinedPendingRegistrations).WithCancellation(cancellation);
                }

                return await _localMessageDispatcher.DispatchAsync(dispatchData, publish, cancellation);
            }
            finally
            {
                _logger?.LogDebug($"End-point '{localEndPoint}': Dispatched message of type {dispatchData.MessageType} locally.");
            }
        }

        #endregion

        public IHandlerRegistration Register(Type messageType, IContextualProvider<IMessageHandler> messageHandlerProvider)
        {
            // This needs to be persistent instead of default to support the persistent registration of message handlers via the message handler pattern without introduction of further dependencies.
            return Register(messageType, messageHandlerProvider, RouteRegistrationOptions.Default);
        }

        public IHandlerRegistration Register(Type messageType, IContextualProvider<IMessageHandler> messageHandlerProvider, RouteRegistrationOptions options)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            var typedDispatcher = GetTypedDispatcher(messageType);
            return new HandlerRegistration(typedDispatcher, messageType, messageHandlerProvider, options);
        }

        private TypedMessageDisaptcher GetTypedDispatcher(Type messageType)
        {
            return _typedDispatchers.GetOrAdd(messageType, t => new TypedMessageDisaptcher(_localMessageDispatcher, _messageRouter, _typeConversion, t));
        }

        private sealed class TypedMessageDisaptcher
        {
            private readonly IMessageDispatcher _localMessageDispatcher;
            private readonly IMessageRouter _messageRouter;
            private readonly ITypeConversion _typeConversion;
            private readonly Type _messageType;
            private readonly string _serializedMessageType;
            private readonly AsyncLock _lock = new AsyncLock();
            private int _count = 0;
            private Task _routeRegistration = null;
            private readonly List<Task> _pendingRegistrations = new List<Task>();

            public TypedMessageDisaptcher(IMessageDispatcher localMessageDispatcher, IMessageRouter messageRouter, ITypeConversion typeConversion, Type messageType)
            {
                _localMessageDispatcher = localMessageDispatcher;
                _messageRouter = messageRouter;
                _typeConversion = typeConversion;
                _messageType = messageType;
                _serializedMessageType = _typeConversion.SerializeType(_messageType);
            }

            public ImmutableList<Task> PendingRegistrations
            {
                get
                {
                    lock (_pendingRegistrations)
                    {
                        if (!_pendingRegistrations.Any())
                        {
                            return ImmutableList<Task>.Empty;
                        }

                        return _pendingRegistrations.ToImmutableList();
                    }
                }
            }

            private async Task<IHandlerRegistration> RegisterHandleInternalAsync(
                IContextualProvider<IMessageHandler> messageHandlerProvider,
                RouteRegistrationOptions options)
            {
                var registration = _localMessageDispatcher.Register(_messageType, messageHandlerProvider);

                try
                {
                    await registration.Initialization;

                    async Task RegisterRouteAsync()
                    {
                        // TODO: Can we cancel this, if the last handler registration cancels?
                        await _messageRouter.RegisterRouteAsync(_serializedMessageType, options, default);
                    }

                    Task routeRegistration;

                    using (await _lock.LockAsync())
                    {
                        if (_count++ == 0)
                        {
                            _routeRegistration = RegisterRouteAsync();
                        }

                        routeRegistration = _routeRegistration;
                    }

                    await routeRegistration;

                    return registration;
                }
                catch
                {
                    await registration.DisposeAsync();

                    throw;
                }
            }

            public async Task<IHandlerRegistration> RegisterHandlerAsync(
                IContextualProvider<IMessageHandler> messageHandlerProvider,
                RouteRegistrationOptions options,
                CancellationToken cancellation)
            {
                var pendingRegistration = RegisterHandleInternalAsync(messageHandlerProvider, options);

                lock (_pendingRegistrations)
                {
                    _pendingRegistrations.Add(pendingRegistration);
                }

                try
                {
                    return await pendingRegistration;
                }
                finally
                {
                    lock (_pendingRegistrations)
                    {
                        _pendingRegistrations.Remove(pendingRegistration);
                    }
                }
            }

            public async Task UnregisterHandlerAsync()
            {
                using (await _lock.LockAsync())
                {
                    if (_count-- == 1)
                    {
                        await _messageRouter.UnregisterRouteAsync(_serializedMessageType, cancellation: default);
                    }
                }
            }
        }

        private sealed class HandlerRegistration : IHandlerRegistration<IMessageHandler>
        {
            private readonly TypedMessageDisaptcher _typedMessageDispatcher;
            private readonly Type _messageType;
            private readonly RouteRegistrationOptions _options;

            private readonly Task<IHandlerRegistration> _initialization;
            private readonly CancellationTokenSource _disposalSource;
            private readonly TaskCompletionSource<object> _disposalTaskSource;
            private volatile Task _disposalTask;
            private readonly object _disposalTaskLock = new object();

            public HandlerRegistration(TypedMessageDisaptcher typedMessageDispatcher,
                                       Type messageType,
                                       IContextualProvider<IMessageHandler> messageHandlerProvider,
                                       RouteRegistrationOptions options)
            {
                _typedMessageDispatcher = typedMessageDispatcher;
                _messageType = messageType;
                Handler = messageHandlerProvider;
                _options = options;

                _disposalSource = new CancellationTokenSource();
                _disposalTaskSource = new TaskCompletionSource<object>();

                _initialization = _typedMessageDispatcher.RegisterHandlerAsync(messageHandlerProvider, options, _disposalSource.Token);
            }

            public Task Initialization => GetExternalInitialization();
            public IContextualProvider<IMessageHandler> Handler { get; }

            private async Task GetExternalInitialization()
            {
                try
                {
                    await _initialization;
                }
                catch (OperationCanceledException) { }
            }

            private async Task DisposeInternalAsync()
            {
                try
                {
                    try
                    {
                        var handlerRegistration = await _initialization;
                        await _typedMessageDispatcher.UnregisterHandlerAsync();
                        await handlerRegistration.DisposeAsync();
                    }
                    catch (OperationCanceledException) { }
                }
                catch (Exception exc)
                {
                    _disposalTaskSource.TrySetException(exc);
                }
                finally
                {
                    _disposalTaskSource.TrySetResult(null);
                    _disposalSource.Dispose();
                }
            }

            public void Dispose()
            {
                if (_disposalTask != null)
                    return;

                lock (_disposalTaskLock)
                {
                    if (_disposalTask == null)
                    {
                        _disposalSource.Cancel();
                        _disposalTask = DisposeInternalAsync();
                    }
                }
            }

            public Task DisposeAsync()
            {
                Dispose();

                return Disposal;
            }

            public Task Disposal => _disposalTaskSource.Task;
        }
    }
}
