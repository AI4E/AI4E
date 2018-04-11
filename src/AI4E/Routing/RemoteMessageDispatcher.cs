/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RemoteMessageDispatcher.cs 
 * Types:           (1) AI4E.Routing.RemoteMessageDispatcher
 *                  (2) AI4E.Routing.RemoteMessageDispatcher.ITypedRemoteMessageDispatcher
 *                  (3) AI4E.Routing.RemoteMessageDispatcher.RequestMessage
 *                  (4) AI4E.Routing.RemoteMessageDispatcher.ResponseMessage
 *                  (5) AI4E.Routing.RemoteMessageDispatcher.TypedRemoteMessageDispatcher'1
 *                  (6) AI4E.Routing.RemoteMessageDispatcher.TypedRemoteMessageDispatcher'1.HandlerRegistration
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
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
using AI4E.Modularity;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace AI4E.Routing
{
    // TODO: Route caching and cache coherency
    public sealed class RemoteMessageDispatcher : IRemoteMessageDispatcher, IAsyncDisposable
    {
        #region Fields

        private readonly IAsyncProcess _receiveProcess;
        private readonly ConcurrentDictionary<Type, ITypedRemoteMessageDispatcher> _typedDispatchers = new ConcurrentDictionary<Type, ITypedRemoteMessageDispatcher>();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteMessageDispatcher> _logger;
        private readonly IEndPointManager _endPointManager;
        private readonly IRouteStore _routeStore;
        private readonly IMessageTypeConversion _messageTypeConversion;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<IDispatchResult>> _responseTable = new ConcurrentDictionary<int, TaskCompletionSource<IDispatchResult>>();
        private readonly JsonSerializer _serializer = new JsonSerializer() { TypeNameHandling = TypeNameHandling.Auto };
        private int _nextSeqNum = 1;

        #endregion

        #region C'tor

        public RemoteMessageDispatcher(IEndPointManager endPointManager,
                                       IRouteStore routeStore,
                                       IMessageTypeConversion messageTypeConversion,
                                       IOptions<RemoteMessagingOptions> optionsAccessor,
                                       IServiceProvider serviceProvider,
                                       ILogger<RemoteMessageDispatcher> logger)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (routeStore == null)
                throw new ArgumentNullException(nameof(routeStore));

            if (messageTypeConversion == null)
                throw new ArgumentNullException(nameof(messageTypeConversion));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var options = optionsAccessor.Value ?? new RemoteMessagingOptions();

            if (options.LocalEndPoint == default)
            {
                throw new ArgumentException("A local end point must be specified to create a remote message dispatcher.");
            }

            _endPointManager = endPointManager;
            _routeStore = routeStore;
            LocalEndPoint = options.LocalEndPoint;
            _messageTypeConversion = messageTypeConversion;
            _serviceProvider = serviceProvider;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcedure);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public RemoteMessageDispatcher(IEndPointManager endPointManager,
                                       IRouteStore routeStore,
                                       IMessageTypeConversion messageTypeConversion,
                                       IOptions<RemoteMessagingOptions> optionsAccessor,
                                       IServiceProvider serviceProvider)
            : this(endPointManager, routeStore, messageTypeConversion, optionsAccessor, serviceProvider, null) { }

        #endregion

        public EndPointRoute LocalEndPoint { get; }

        #region Initialization

        private readonly AsyncInitializationHelper _initializationHelper;

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _endPointManager.AddEndPointAsync(LocalEndPoint, cancellation);
            await _receiveProcess.StartAsync();
        }

        #endregion

        #region Disposal

        private readonly AsyncDisposeHelper _disposeHelper;

        public Task Disposal => _disposeHelper.Disposal;

        private async Task DisposeInternalAsync()
        {
            // Cancel the initialization
            await _initializationHelper.CancelAsync();

            try
            {
                await _receiveProcess.TerminateAsync();
            }
            finally
            {
                await _endPointManager.RemoveEndPointAsync(LocalEndPoint, cancellation: default);
            }
        }

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        #endregion

        #region Receive Process

        private async Task ReceiveProcedure(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Started receive procedure for end-point {LocalEndPoint}");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var incoming = await _endPointManager.ReceiveAsync(LocalEndPoint, cancellation);

                    _logger?.LogDebug($"End-point '{LocalEndPoint}': Received message.");

                    var message = default(object);

                    using (var stream = incoming.PopFrame().OpenStream())
                    using (var reader = new StreamReader(stream))
                    {
                        message = _serializer.Deserialize(reader, typeof(object));
                    }

                    switch (message)
                    {
                        case RequestMessage request:
                            Task.Run(() => ProcessRequestAsync(request, incoming)).HandleExceptions();
                            break;

                        case ResponseMessage response:
                            Task.Run(() => ProcessResponseAsync(response)).HandleExceptions();
                            break;

                        default:
                            _logger?.LogWarning($"End-point '{LocalEndPoint}': Received bad message that is either of an unkown type or could not be deserialized.");
                            break;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"End-point '{LocalEndPoint}': Exception while processing incoming message.");
                }
            }
        }

        private async Task ProcessRequestAsync(RequestMessage request, IMessage requestMessage)
        {
            _logger?.LogDebug($"End-point '{LocalEndPoint}': Processing request message with seq-num '{request.SeqNum}'.");

            var dispatchResult = default(IDispatchResult);

            try
            {
                dispatchResult = await DispatchLocalAsync(request.MessageType, request.Message, request.Context, request.Publish, cancellation: default);
            }
            catch (Exception exc)
            {
                dispatchResult = new FailureDispatchResult(exc);
            }

            var response = new ResponseMessage
            {
                SeqNum = GetNextSeqNum(),
                CorrNum = request.SeqNum,
                DispatchResult = dispatchResult
            };

            var message = new Message();

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new StreamWriter(stream))
            {
                _serializer.Serialize(writer, response, typeof(object));
            }

            requestMessage.PushFrame();

            await _endPointManager.SendAsync(message, requestMessage, cancellation: default);
        }

        private Task ProcessResponseAsync(ResponseMessage response)
        {
            _logger?.LogDebug($"End-point '{LocalEndPoint}': Processing response message for seq-num '{response.CorrNum}'.");

            if (_responseTable.TryRemove(response.CorrNum, out var tcs))
            {
                tcs.TrySetResult(response.DispatchResult);
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Routing

        private async Task RegisterRouteAsync(string messageType, CancellationToken cancellation)
        {
            await _routeStore.AddRouteAsync(LocalEndPoint, messageType, cancellation);
        }

        private async Task UnregisterRouteAsync(string messageType, CancellationToken cancellation)
        {
            await _routeStore.RemoveRouteAsync(LocalEndPoint, messageType, cancellation);
        }

        private Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            return _routeStore.GetRoutesAsync(messageType, cancellation);
        }

        #endregion

        #region Dispatch

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, EndPointRoute route, CancellationToken cancellation = default)
        {
            return DispatchAsync(typeof(TMessage), message, context, publish, route, cancellation);
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            return DispatchAsync(typeof(TMessage), message, context, publish, route: default, cancellation);
        }

        public Task<IDispatchResult> DispatchLocalAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            return DispatchLocalAsync(typeof(TMessage), message, context, publish, cancellation);
        }

        public async Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, EndPointRoute route, CancellationToken cancellation = default)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (route != null)
            {
                return await DispatchToEndPointAsync(messageType, message, context, publish, route, cancellation);
            }

            return await DispatchAsync(messageType, message, context, publish, cancellation);
        }

        public async Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            var currType = messageType;
            var tasks = new List<Task<IDispatchResult>>();

            var handledRoutes = new HashSet<EndPointRoute>();

            do
            {
                Debug.Assert(currType != null);

                var routes = new HashSet<EndPointRoute>((await GetRoutesAsync(_messageTypeConversion.SerializeMessageType(messageType), cancellation)));
                routes.ExceptWith(handledRoutes);
                handledRoutes.UnionWith(routes);

                if (routes.Any())
                {
                    if (!publish)
                    {
                        var route = routes.Last();

                        if (route.Equals(LocalEndPoint))
                        {
                            return await DispatchLocalAsync(messageType, message, context, publish, cancellation);
                        }

                        return await DispatchToEndPointAsync(messageType, message, context, publish, routes.Last(), cancellation);
                    }

                    foreach (var route in routes)
                    {
                        if (route.Equals(LocalEndPoint))
                        {
                            tasks.Add(DispatchLocalAsync(messageType, message, context, publish, cancellation));
                        }
                        else
                        {
                            tasks.Add(DispatchToEndPointAsync(messageType, message, context, publish, route, cancellation));
                        }
                    }
                }
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);

            if (tasks.Count == 0)
            {
                // When publishing a message and no handlers are available, this is a success.
                if (publish)
                {
                    return new SuccessDispatchResult();
                }

                // When dispatching a message and no handlers are available, this is a failure.
                return new DispatchFailureDispatchResult(messageType);
            }

            if (tasks.Count == 1)
            {
                return await tasks[0];
            }

            return new AggregateDispatchResult(await Task.WhenAll(tasks));
        }

        public async Task<IDispatchResult> DispatchLocalAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation)
        {
            _logger?.LogInformation($"End-point '{LocalEndPoint}': Dispatching message of type {messageType.FullName} locally.");

            var currType = messageType;
            var tasks = new List<Task<IDispatchResult>>();

            do
            {
                Debug.Assert(currType != null);

                if (TryGetTypedDispatcher(currType, out var dispatcher))
                {
                    if (!publish)
                    {
                        var dispatchResult = await dispatcher.DispatchAsync(message, context, publish, cancellation);

                        if (!(dispatchResult is DispatchFailureDispatchResult))
                        {
                            return dispatchResult;
                        }
                    }
                    else
                    {
                        tasks.Add(dispatcher.DispatchAsync(message, context, publish, cancellation));
                    }
                }
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);

            if (!tasks.Any())
            {
                // When publishing a message and no handlers are available, this is a success.
                if (publish)
                {
                    return new SuccessDispatchResult();
                }

                // When dispatching a message and no handlers are available, this is a failure.
                return new DispatchFailureDispatchResult(messageType);
            }

            if (tasks.Count == 1)
            {
                return await tasks[0];
            }

            return new AggregateDispatchResult(await Task.WhenAll(tasks));
        }

        private async Task<IDispatchResult> DispatchToEndPointAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, EndPointRoute remoteEndPoint, CancellationToken cancellation)
        {
            // This does short-curcuit the dispatch to the remote end-point. 
            // Any possible replicates do not get any chance to receive the message. 
            // => Requests are kept local to the machine.
            if (remoteEndPoint == LocalEndPoint)
            {
                return await DispatchLocalAsync(messageType, message, context, publish, cancellation);
            }

            _logger?.LogInformation($"End-point '{LocalEndPoint}': Dispatching message of type {messageType.FullName} to end-point {remoteEndPoint}.");

            var seqNum = GetNextSeqNum();
            var tcs = new TaskCompletionSource<IDispatchResult>();

            while (!_responseTable.TryAdd(seqNum, tcs))
            {
                seqNum = GetNextSeqNum();
            }

            cancellation.Register(() =>
            {
                tcs.TrySetResult(new CancelledDispatchResult());
                _responseTable.TryRemove(seqNum, out _);
            });

            // The operation may be cancelled in the mean time.
            if (tcs.Task.IsCompleted)
            {
                _responseTable.TryRemove(seqNum, out _);
            }
            else
            {
                var request = new RequestMessage
                {
                    SeqNum = seqNum,
                    MessageType = messageType,
                    Message = message,
                    Context = context,
                    Publish = publish
                };

                var msg = new Message();

                using (var stream = msg.PushFrame().OpenStream())
                using (var writer = new StreamWriter(stream))
                {
                    _serializer.Serialize(writer, request, typeof(object));
                }

                await _endPointManager.SendAsync(msg, remoteEndPoint, LocalEndPoint, cancellation);
            }

            return await tcs.Task;
        }

        #endregion

        public IHandlerRegistration<IMessageHandler<TMessage>> Register<TMessage>(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            var typedDispatcher = GetTypedDispatcher<TMessage>();

            Debug.Assert(typedDispatcher != null);

            return typedDispatcher.Register(messageHandlerProvider);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        #region Typed Dispatcher

        private bool TryGetTypedDispatcher(Type messageType, out ITypedRemoteMessageDispatcher value)
        {
            Debug.Assert(messageType != null);

            var result = _typedDispatchers.TryGetValue(messageType, out value);

            Debug.Assert(!result || value != null);
            Debug.Assert(!result || value.MessageType == messageType);
            return result;
        }

        private TypedRemoteMessageDispatcher<TMessage> GetTypedDispatcher<TMessage>()
        {
            return (TypedRemoteMessageDispatcher<TMessage>)
                   _typedDispatchers.GetOrAdd(
                       typeof(TMessage),
                       _ => new TypedRemoteMessageDispatcher<TMessage>(this, _serviceProvider, _messageTypeConversion));
        }

        private interface ITypedRemoteMessageDispatcher
        {
            Type MessageType { get; }

            Task<IDispatchResult> DispatchAsync(object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation);
        }

        private sealed class TypedRemoteMessageDispatcher<TMessage> : ITypedRemoteMessageDispatcher
        {
            private readonly HandlerRegistry<IMessageHandler<TMessage>> _registry = new HandlerRegistry<IMessageHandler<TMessage>>();
            private readonly RemoteMessageDispatcher _dispatcher;
            private readonly IServiceProvider _serviceProvider;
            private readonly IMessageTypeConversion _messageTypeConversion;
            private readonly AsyncLock _lock = new AsyncLock();
            private volatile ImmutableList<Task> _registrationTasks = ImmutableList<Task>.Empty;

            public TypedRemoteMessageDispatcher(RemoteMessageDispatcher dispatcher, IServiceProvider serviceProvider, IMessageTypeConversion messageTypeConversion)
            {
                if (dispatcher == null)
                    throw new ArgumentNullException(nameof(dispatcher));

                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                if (messageTypeConversion == null)
                    throw new ArgumentNullException(nameof(messageTypeConversion));

                _dispatcher = dispatcher;
                _serviceProvider = serviceProvider;
                _messageTypeConversion = messageTypeConversion;
            }

            #region Routing

            private Task RegisterRouteAsync(string messageType, CancellationToken cancellation)
            {
                return _dispatcher.RegisterRouteAsync(messageType, cancellation);
            }

            private Task UnregisterRouteAsync(string messageType, CancellationToken cancellation)
            {
                return _dispatcher.UnregisterRouteAsync(messageType, cancellation);
            }

            #endregion

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

            private Task<IDispatchResult> DispatchSingleHandlerAsync(IContextualProvider<IMessageHandler<TMessage>> handler,
                                                                     TMessage message,
                                                                     DispatchValueDictionary context,
                                                                     CancellationToken cancellation)
            {
                // TODO: Cancellation

                Debug.Assert(message != null);
                Debug.Assert(handler != null);

                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        return handler.ProvideInstance(scope.ServiceProvider).HandleAsync(message, context);
                    }
                    catch (ConcurrencyException)
                    {
                        return Task.FromResult<IDispatchResult>(new ConcurrencyIssueDispatchResult());
                    }
                    catch (Exception exc)
                    {
                        return Task.FromResult<IDispatchResult>(new FailureDispatchResult(exc));
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
                                await RegisterRouteAsync(SerializedMessageType, cancellation: default);
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
                            await UnregisterRouteAsync(SerializedMessageType, cancellation: default);
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

            public string SerializedMessageType => _messageTypeConversion.SerializeMessageType(MessageType);

            private sealed class HandlerRegistration : IHandlerRegistration<IMessageHandler<TMessage>>, IAsyncDisposable
            {
                private readonly Func<IContextualProvider<IMessageHandler<TMessage>>, Task> _unregistration;

                public HandlerRegistration(IContextualProvider<IMessageHandler<TMessage>> handler,
                                           Func<IContextualProvider<IMessageHandler<TMessage>>, Task> registration,
                                           Func<IContextualProvider<IMessageHandler<TMessage>>, Task> unregistration)
                {
                    Debug.Assert(handler != null);
                    Debug.Assert(registration != null);
                    Debug.Assert(unregistration != null);

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

        #region Messages

        private sealed class RequestMessage
        {
            public int SeqNum { get; set; }

            public Type MessageType { get; set; }

            public object Message { get; set; }

            public DispatchValueDictionary Context { get; set; }

            public bool Publish { get; set; }
        }

        private sealed class ResponseMessage
        {
            public int SeqNum { get; set; }

            public int CorrNum { get; set; }

            public IDispatchResult DispatchResult { get; set; }
        }

        #endregion
    }
}
