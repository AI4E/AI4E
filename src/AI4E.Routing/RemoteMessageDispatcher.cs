/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RemoteMessageDispatcher.cs 
 * Types:           (1) AI4E.Routing.RemoteMessageDispatcher
 *                  (2) AI4E.Routing.RemoteMessageDispatcher.ITypedRemoteMessageDispatcher
 *                  (5) AI4E.Routing.RemoteMessageDispatcher.TypedRemoteMessageDispatcher'1
 *                  (6) AI4E.Routing.RemoteMessageDispatcher.TypedRemoteMessageDispatcher'1.HandlerRegistration
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   31.07.2018 
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

        private readonly ConcurrentDictionary<Type, ITypedRemoteMessageDispatcher> _typedDispatchers = new ConcurrentDictionary<Type, ITypedRemoteMessageDispatcher>();
        private readonly JsonSerializer _serializer = new JsonSerializer() { TypeNameHandling = TypeNameHandling.Auto };

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

            _messageRouter = messageRouterFactory.CreateMessageRouter(new SerializedMessageHandler(this));
            _typeConversion = typeConversion;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        #endregion

        [Obsolete("Use GetLocalEndPointAsync(CancellationToken)")]
        public EndPointRoute LocalEndPoint => GetLocalEndPointAsync(cancellation: default)
                                                .ConfigureAwait(false)
                                                .GetAwaiter()
                                                .GetResult();

        public ValueTask<EndPointRoute> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return _messageRouter.GetLocalEndPointAsync(cancellation);
        }

        private sealed class SerializedMessageHandler : ISerializedMessageHandler
        {
            private readonly RemoteMessageDispatcher _owner;
            private readonly ITypeConversion _typeConversion;

            public SerializedMessageHandler(RemoteMessageDispatcher owner)
            {
                Assert(owner != null);

                _owner = owner;
                _typeConversion = _owner._typeConversion;
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

                var (message, dispatchValues) = _owner.DeserializeDispatchValues(serializedMessage);

                var dispatchResult = await _owner.DispatchLocalAsync(messageType, message, dispatchValues, publish, cancellation);

                var response = new Message();
                _owner.SerializeDispatchResult(response, dispatchResult);
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

        private void SerializeDispatchValues(IMessage message, object messageX, DispatchValueDictionary dispatchValues)
        {
            Assert(message != null);
            Assert(dispatchValues != null);

            var tuple = new Tuple<object, DispatchValueDictionary>(messageX, dispatchValues);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new StreamWriter(stream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                _serializer.Serialize(jsonWriter, tuple, typeof(Tuple<object, DispatchValueDictionary>));
            }
        }

        private (object message, DispatchValueDictionary dispatchValues) DeserializeDispatchValues(IMessage message)
        {
            Assert(message != null);

            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var tuple = _serializer.Deserialize<Tuple<object, DispatchValueDictionary>>(jsonReader);

                return (tuple.Item1, tuple.Item2);
            }
        }

        #region Dispatch

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, EndPointRoute route, CancellationToken cancellation = default)
        {
            return DispatchAsync(typeof(TMessage), message, context, publish, route, cancellation);
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            return DispatchAsync(typeof(TMessage), message, context, publish, endPoint: default, cancellation);
        }

        public Task<IDispatchResult> DispatchLocalAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            return DispatchLocalAsync(typeof(TMessage), message, context, publish, cancellation);
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType,
                                                   object message,
                                                   DispatchValueDictionary dispatchValues,
                                                   bool publish,
                                                   EndPointRoute endPoint,
                                                   CancellationToken cancellation = default)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (dispatchValues == null)
                throw new ArgumentNullException(nameof(dispatchValues));

            if (endPoint != null)
            {
                return InternalDispatchAsync(messageType, message, dispatchValues, publish, endPoint, cancellation);
            }

            return InternalDispatchAsync(messageType, message, dispatchValues, publish, cancellation);
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType,
                                                   object message,
                                                   DispatchValueDictionary dispatchValues,
                                                   bool publish,
                                                   CancellationToken cancellation = default)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (dispatchValues == null)
                throw new ArgumentNullException(nameof(dispatchValues));

            return InternalDispatchAsync(messageType, message, dispatchValues, publish, cancellation);
        }

        private async Task<IDispatchResult> InternalDispatchAsync(Type messageType,
                                                                  object message,
                                                                  DispatchValueDictionary dispatchValues,
                                                                  bool publish,
                                                                  EndPointRoute endPoint,
                                                                  CancellationToken cancellation)
        {
            var route = _typeConversion.SerializeType(messageType);
            var serializedMessage = new Message();

            SerializeDispatchValues(serializedMessage, message, dispatchValues);

            var serializedResult = await _messageRouter.RouteAsync(route, serializedMessage, publish, endPoint, cancellation);
            var result = DeserializeDispatchResult(serializedResult);

            return result;
        }

        private async Task<IDispatchResult> InternalDispatchAsync(Type messageType,
                                                                  object message,
                                                                  DispatchValueDictionary dispatchValues,
                                                                  bool publish,
                                                                  CancellationToken cancellation)
        {
            var routes = GetRoutes(messageType);
            var serializedMessage = new Message();
            SerializeDispatchValues(serializedMessage, message, dispatchValues);

            var serializedResults = await _messageRouter.RouteAsync(routes, serializedMessage, publish, cancellation);
            var results = serializedResults.Select(p => DeserializeDispatchResult(p)).ToList();

            if (results.Count == 0)
            {
                if (publish)
                {
                    return new SuccessDispatchResult();
                }

                return new DispatchFailureDispatchResult(messageType);
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

        public async Task<IDispatchResult> DispatchLocalAsync(Type messageType,
                                                              object message,
                                                              DispatchValueDictionary dispatchValues,
                                                              bool publish,
                                                              CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            _logger?.LogInformation($"End-point '{localEndPoint}': Dispatching message of type {messageType.FullName} locally.");

            IDispatchResult Result(IDispatchResult result)
            {
                _logger?.LogDebug($"End-point '{localEndPoint}': Dispatched message of type {messageType.FullName} locally.");

                return result;
            }

            var currType = messageType;
            var tasks = new List<Task<IDispatchResult>>();

            do
            {
                Assert(currType != null);

                if (TryGetTypedDispatcher(currType, out var dispatcher))
                {
                    if (!publish)
                    {
                        var dispatchResult = await dispatcher.DispatchAsync(message, dispatchValues, publish, cancellation);

                        if (!(dispatchResult is DispatchFailureDispatchResult))
                        {
                            return Result(dispatchResult);
                        }
                    }
                    else
                    {
                        tasks.Add(dispatcher.DispatchAsync(message, dispatchValues, publish, cancellation));
                    }
                }
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);

            if (!tasks.Any())
            {
                // When publishing a message and no handlers are available, this is a success.
                if (publish)
                {
                    return Result(new SuccessDispatchResult());
                }

                // When dispatching a message and no handlers are available, this is a failure.
                return Result(new DispatchFailureDispatchResult(messageType));
            }

            if (tasks.Count == 1)
            {
                return Result(await tasks[0]);
            }

            return Result(new AggregateDispatchResult(await Task.WhenAll(tasks)));
        }


        #endregion

        public IHandlerRegistration<IMessageHandler<TMessage>> Register<TMessage>(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            var typedDispatcher = GetTypedDispatcher<TMessage>();

            Assert(typedDispatcher != null);

            return typedDispatcher.Register(messageHandlerProvider);
        }

        #region Typed Dispatcher

        private bool TryGetTypedDispatcher(Type messageType, out ITypedRemoteMessageDispatcher value)
        {
            Assert(messageType != null);

            var result = _typedDispatchers.TryGetValue(messageType, out value);

            Assert(!result || value != null);
            Assert(!result || value.MessageType == messageType);
            return result;
        }

        private TypedRemoteMessageDispatcher<TMessage> GetTypedDispatcher<TMessage>()
        {
            return (TypedRemoteMessageDispatcher<TMessage>)
                   _typedDispatchers.GetOrAdd(
                       typeof(TMessage),
                       _ => new TypedRemoteMessageDispatcher<TMessage>(_messageRouter,
                                                                       _typeConversion,
                                                                       _serviceProvider));
        }

        private interface ITypedRemoteMessageDispatcher
        {
            Type MessageType { get; }

            Task<IDispatchResult> DispatchAsync(object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation);
        }

        private sealed class TypedRemoteMessageDispatcher<TMessage> : ITypedRemoteMessageDispatcher
        {
            private readonly HandlerRegistry<IMessageHandler<TMessage>> _registry = new HandlerRegistry<IMessageHandler<TMessage>>();
            private readonly IMessageRouter _messageRouter;
            private readonly IServiceProvider _serviceProvider;
            private readonly ITypeConversion _typeConversion;
            private readonly AsyncLock _lock = new AsyncLock();
            private volatile ImmutableList<Task> _registrationTasks = ImmutableList<Task>.Empty;

            public TypedRemoteMessageDispatcher(IMessageRouter messageRouter,
                                                ITypeConversion typeConversion,
                                                IServiceProvider serviceProvider)
            {
                if (messageRouter == null)
                    throw new ArgumentNullException(nameof(messageRouter));

                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                if (typeConversion == null)
                    throw new ArgumentNullException(nameof(typeConversion));

                _messageRouter = messageRouter;
                _serviceProvider = serviceProvider;
                _typeConversion = typeConversion;
            }

            public Task<IDispatchResult> DispatchAsync(object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (!(message is TMessage typedMessage))
                {
                    throw new ArgumentException($"The argument must be of type '{ typeof(TMessage).FullName }' or a derived type.");
                }

                return DispatchAsync(typedMessage, context, publish, cancellation);
            }

            public async Task<IDispatchResult> DispatchAsync(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (context == null)
                    throw new ArgumentNullException(nameof(context));

                var tasksToWait = _registrationTasks; // Volatile read op
                await Task.WhenAll(tasksToWait);

                if (publish)
                {
                    IEnumerable<IContextualProvider<IMessageHandler<TMessage>>> handlers;
                    using (await _lock.LockAsync())
                    {
                        handlers = _registry.Handlers;
                    }

                    var dispatchResults = await Task.WhenAll(handlers.Select(p => DispatchSingleHandlerAsync(p, message, context, cancellation)));

                    return new AggregateDispatchResult(dispatchResults);
                }
                else
                {
                    bool result;
                    IContextualProvider<IMessageHandler<TMessage>> handler;

                    using (await _lock.LockAsync())
                    {
                        result = _registry.TryGetHandler(out handler);
                    }

                    if (result)
                    {
                        return await DispatchSingleHandlerAsync(handler, message, context, cancellation);
                    }

                    return new DispatchFailureDispatchResult(typeof(TMessage));
                }
            }

            private async Task<IDispatchResult> DispatchSingleHandlerAsync(IContextualProvider<IMessageHandler<TMessage>> handlerProvider,
                                                                           TMessage message,
                                                                           DispatchValueDictionary context,
                                                                           CancellationToken cancellation)
            {
                // TODO: Cancellation

                Assert(message != null);
                Assert(handlerProvider != null);

                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        // TODO: If instancing the handler failed or the returned handler is null, 
                        //       do we return a dispatchfailure result with a proper error message instead?
                        var handler = handlerProvider.ProvideInstance(scope.ServiceProvider);

                        if (handler == null)
                            return new FailureDispatchResult();

                        return await handler.HandleAsync(message, context);
                    }
                    catch (ConcurrencyException)
                    {
                        return new ConcurrencyIssueDispatchResult();
                    }
                    catch (Exception exc)
                    {
                        return new FailureDispatchResult(exc);
                    }
                }
            }

            public IHandlerRegistration<IMessageHandler<TMessage>> Register(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
            {
                return new HandlerRegistration(messageHandlerProvider, RegisterInternalAsync, UnregisterInternalAsync);
            }

            private Task RegisterInternalAsync(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
            {
                async Task DoRegistrationAsync()
                {
                    using (await _lock.LockAsync())
                    {
                        var handlerCount = _registry.Handlers.Count();
                        _registry.Register(messageHandlerProvider);

                        if (handlerCount == 0)
                        {
                            try
                            {
                                await _messageRouter.RegisterRouteAsync(SerializedMessageType, cancellation: default);
                            }
                            catch
                            {
                                _registry.Unregister(messageHandlerProvider);

                                throw;
                            }
                        }
                    }
                }

                var registration = DoRegistrationAsync();
                AddRegistration(registration);
                var registrationAndCleanup = AwaitAndCleanupAsync(registration);
                return registrationAndCleanup;
            }

            private Task UnregisterInternalAsync(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
            {
                async Task DoUnregistrationAsync()
                {
                    using (await _lock.LockAsync())
                    {
                        var handlers = _registry.Handlers;

                        if (handlers.Count() == 1 && handlers.First() == messageHandlerProvider)
                        {
                            await _messageRouter.UnregisterRouteAsync(SerializedMessageType, cancellation: default);
                        }

                        _registry.Unregister(messageHandlerProvider);
                    }
                }

                var unregistration = DoUnregistrationAsync();
                AddRegistration(unregistration);
                var unregistrationAndCleanup = AwaitAndCleanupAsync(unregistration);
                return unregistrationAndCleanup;
            }

            private void AddRegistration(Task registration)
            {
                ImmutableList<Task> current = _registrationTasks, // Volatile read op
                                    start,
                                    desired;

                do
                {
                    start = current;
                    desired = start.Add(registration);
                    current = Interlocked.CompareExchange(ref _registrationTasks, desired, start);
                }
                while (start != current);
            }

            private void CleanupRegistrations()
            {
                ImmutableList<Task> current = _registrationTasks, // Volatile read op
                                    start,
                                    desired;

                do
                {
                    start = current;
                    desired = start.RemoveAll(p => !p.IsRunning());
                    current = Interlocked.CompareExchange(ref _registrationTasks, desired, start);
                }
                while (start != current);
            }

            private async Task AwaitAndCleanupAsync(Task registration)
            {
                try
                {
                    await registration;
                }
                finally
                {
                    CleanupRegistrations();
                }
            }

            public Type MessageType => typeof(TMessage);

            public string SerializedMessageType => _typeConversion.SerializeType(MessageType);

            private sealed class HandlerRegistration : IHandlerRegistration<IMessageHandler<TMessage>>, IAsyncInitialization, IAsyncDisposable
            {
                private readonly Func<IContextualProvider<IMessageHandler<TMessage>>, Task> _unregistration;

                public HandlerRegistration(IContextualProvider<IMessageHandler<TMessage>> handler,
                                           Func<IContextualProvider<IMessageHandler<TMessage>>, Task> registration,
                                           Func<IContextualProvider<IMessageHandler<TMessage>>, Task> unregistration)
                {
                    Assert(handler != null);
                    Assert(registration != null);
                    Assert(unregistration != null);

                    Handler = handler;
                    _unregistration = unregistration;
                    Initialization = registration(handler);
                }

                public Task Initialization { get; }

                public IContextualProvider<IMessageHandler<TMessage>> Handler { get; }

                #region Disposal

                private Task _disposal;
                private readonly TaskCompletionSource<byte> _disposalSource = new TaskCompletionSource<byte>();
                private readonly object _lock = new object();

                public Task Disposal => _disposalSource.Task;

                private async Task DisposeInternalAsync()
                {
                    try
                    {
                        await _unregistration(Handler);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception exc)
                    {
                        _disposalSource.SetException(exc);
                        return;
                    }

                    _disposalSource.SetResult(0);
                }

                public void Dispose()
                {
                    lock (_lock)
                    {
                        if (_disposal == null)
                            _disposal = DisposeInternalAsync();
                    }
                }

                public Task DisposeAsync()
                {
                    Dispose();
                    return Disposal;
                }

                #endregion

                public Task Cancellation => Disposal;

                public void Cancel()
                {
                    Dispose();
                }
            }
        }

        #endregion
    }
}
