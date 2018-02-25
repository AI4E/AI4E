/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RemoteMessageDispatcher.cs 
 * Types:           (1) AI4E.Modularity.RemoteMessageDispatcher
 *                  (2) AI4E.Modularity.RemoteMessageDispatcher.ITypedRemoteMessageDispatcher
 *                  (3) AI4E.Modularity.RemoteMessageDispatcher.RequestMessage
 *                  (4) AI4E.Modularity.RemoteMessageDispatcher.ResponseMessage
 *                  (5) AI4E.Modularity.RemoteMessageDispatcher.TypedRemoteMessageDispatcher'1
 *                  (6) AI4E.Modularity.RemoteMessageDispatcher.TypedRemoteMessageDispatcher'1.HandlerRegistration
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   12.02.2018 
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.DispatchResults;
using AI4E.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace AI4E.Modularity
{
    public sealed class RemoteMessageDispatcher : IRemoteMessageDispatcher
    {
        #region Fields

        private readonly IEndPointRouter _moduleCoordination;
        private readonly AsyncProcess _receiveProcess;
        private readonly ConcurrentDictionary<Type, ITypedRemoteMessageDispatcher> _typedDispatchers = new ConcurrentDictionary<Type, ITypedRemoteMessageDispatcher>();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteMessageDispatcher> _logger;
        private readonly IMessageTypeConversion _messageTypeConversion;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<IDispatchResult>> _responseTable = new ConcurrentDictionary<int, TaskCompletionSource<IDispatchResult>>();
        private readonly JsonSerializer _serializer = new JsonSerializer() { TypeNameHandling = TypeNameHandling.Auto };
        private int _nextSeqNum = 1;

        #endregion

        #region C'tor

        public RemoteMessageDispatcher(IEndPointRouter moduleCoordination,
                                       IMessageTypeConversion messageTypeConversion,
                                       IServiceProvider serviceProvider,
                                       ILogger<RemoteMessageDispatcher> logger)
        {
            if (moduleCoordination == null)
                throw new ArgumentNullException(nameof(moduleCoordination));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (messageTypeConversion == null)
                throw new ArgumentNullException(nameof(messageTypeConversion));

            _moduleCoordination = moduleCoordination;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _messageTypeConversion = messageTypeConversion;
            _receiveProcess = new AsyncProcess(ReceiveProcedure);
            _receiveProcess.StartExecution();
        }

        public RemoteMessageDispatcher(IEndPointRouter moduleCoordination,
                                       IMessageTypeConversion messageTypeConversion,
                                       IServiceProvider serviceProvider)
            : this(moduleCoordination, messageTypeConversion, serviceProvider, null) { }

        #endregion

        public EndPointRoute LocalEndPoint => _moduleCoordination.LocalEndPoint;

        #region Receive Process

        private async Task ReceiveProcedure(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Started receive procedure for end-point {LocalEndPoint}");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var incoming = await _moduleCoordination.ReceiveAsync(cancellation);

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
            _logger?.LogDebug($"End-point '{LocalEndPoint}': Processing request of message with seq-num '{request.SeqNum}'.");

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

            await _moduleCoordination.SendAsync(message, requestMessage, cancellation: default);
        }

        private Task ProcessResponseAsync(ResponseMessage response)
        {
            _logger?.LogDebug($"End-point '{LocalEndPoint}': Processing response for message with seq-num '{response.CorrNum}'.");

            if (_responseTable.TryRemove(response.CorrNum, out var tcs))
            {
                tcs.TrySetResult(response.DispatchResult);
            }

            return Task.CompletedTask;
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

                var routes = new HashSet<EndPointRoute>((await _moduleCoordination.GetRoutesAsync(_messageTypeConversion.SerializeMessageType(messageType), cancellation)));
                routes.ExceptWith(handledRoutes);
                handledRoutes.UnionWith(routes);

                if (routes.Any())
                {
                    if (!publish)
                    {
                        var route = routes.Last();

                        if (route.Equals(_moduleCoordination.LocalEndPoint))
                        {
                            return await DispatchLocalAsync(messageType, message, context, publish, cancellation);
                        }

                        return await DispatchToEndPointAsync(messageType, message, context, publish, routes.Last(), cancellation);
                    }

                    foreach (var route in routes)
                    {
                        if (route.Equals(_moduleCoordination.LocalEndPoint))
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

                await _moduleCoordination.SendAsync(msg, remoteEndPoint, cancellation);
            }

            return await tcs.Task;
        }

        #endregion

        public Task<IHandlerRegistration<IMessageHandler<TMessage>>> RegisterAsync<TMessage>(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider,
                                                                                             CancellationToken cancellation = default)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            var typedDispatcher = GetTypedDispatcher<TMessage>();

            Debug.Assert(typedDispatcher != null);

            return typedDispatcher.RegisterAsync(messageHandlerProvider, cancellation);
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
                       _ => new TypedRemoteMessageDispatcher<TMessage>(_moduleCoordination, _serviceProvider, _messageTypeConversion));
        }

        private interface ITypedRemoteMessageDispatcher
        {
            Type MessageType { get; }

            Task<IDispatchResult> DispatchAsync(object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation);
        }

        private sealed class TypedRemoteMessageDispatcher<TMessage> : ITypedRemoteMessageDispatcher
        {
            private readonly IEndPointRouter _moduleCoordination;
            private readonly HandlerRegistry<IMessageHandler<TMessage>> _registry = new HandlerRegistry<IMessageHandler<TMessage>>();
            private readonly IServiceProvider _serviceProvider;
            private readonly IMessageTypeConversion _messageTypeConversion;
            private readonly AsyncLock _lock = new AsyncLock();

            public TypedRemoteMessageDispatcher(IEndPointRouter moduleCoordination, IServiceProvider serviceProvider, IMessageTypeConversion messageTypeConversion)
            {
                if (moduleCoordination == null)
                    throw new ArgumentNullException(nameof(moduleCoordination));

                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));
                if (messageTypeConversion == null)
                    throw new ArgumentNullException(nameof(messageTypeConversion));
                _moduleCoordination = moduleCoordination;
                _serviceProvider = serviceProvider;
                _messageTypeConversion = messageTypeConversion;
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

                if (publish)
                {
                    var dispatchResults = await Task.WhenAll(_registry.Handlers.Select(p => DispatchSingleHandlerAsync(p, message, context, cancellation)));

                    return new AggregateDispatchResult(dispatchResults);
                }
                else
                {
                    if (_registry.TryGetHandler(out var handler))
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

            public async Task<IHandlerRegistration<IMessageHandler<TMessage>>> RegisterAsync(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider,
                                                                                            CancellationToken cancellation)
            {
                using (await _lock.LockAsync())
                {
                    var handlerCount = _registry.Handlers.Count();

                    _registry.Register(messageHandlerProvider);

                    var registration = new HandlerRegistration(this, messageHandlerProvider);

                    if (handlerCount == 0)
                    {
                        try
                        {
                            await _moduleCoordination.RegisterRouteAsync(SerializedMessageType, cancellation);
                        }
                        catch
                        {
                            _registry.Unregister(messageHandlerProvider);

                            throw;
                        }
                    }

                    return registration;
                }
            }

            public Type MessageType => typeof(TMessage);

            public string SerializedMessageType => _messageTypeConversion.SerializeMessageType(MessageType);

            private sealed class HandlerRegistration : IHandlerRegistration<IMessageHandler<TMessage>>
            {
                private readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();
                private readonly object _lock = new object();
                private readonly TypedRemoteMessageDispatcher<TMessage> _dispatcher;
                private Task _completion;

                public HandlerRegistration(TypedRemoteMessageDispatcher<TMessage> dispatcher,
                                           IContextualProvider<IMessageHandler<TMessage>> handler)
                {
                    Debug.Assert(dispatcher != null);
                    Debug.Assert(handler != null);

                    _dispatcher = dispatcher;
                    Handler = handler;
                }

                public IContextualProvider<IMessageHandler<TMessage>> Handler { get; }

                public Task Completion => _completionSource.Task;

                public void Complete()
                {
                    lock (_lock)
                    {
                        if (_completion == null)
                            _completion = CompleteAsync();
                    }
                }

                private async Task CompleteAsync()
                {
                    try
                    {
                        using (await _dispatcher._lock.LockAsync())
                        {
                            var handlers = _dispatcher._registry.Handlers;

                            if (handlers.Count() == 1 && handlers.First() == Handler)
                            {
                                await _dispatcher._moduleCoordination.UnregisterRouteAsync(_dispatcher.SerializedMessageType, cancellation: default);
                            }

                            _dispatcher._registry.Unregister(Handler);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception exc)
                    {
                        _completionSource.TrySetException(exc);
                    }

                    _completionSource.TrySetResult(null);
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
