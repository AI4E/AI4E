using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    // TODO: (1) The receive process must be throttled. If not, the receive process will execute an infinite loop because the Task.Run call is not awaited.
    //           This can be solved most clean if the LogicalServerEndPoints receive Api is replaced with an api like in the LogicalEndPoint.
    //       (2) A GC must be implemented that removes clients that's sessions are terminated. 
    //       (3) Rename
    //       (4) Lease length shall be configurable and synced with the client.
    public sealed class ClientManager : IAsyncDisposable
    {
        private readonly TimeSpan _leaseLength = TimeSpan.FromSeconds(30); // TODO: This should be configurable.

        private readonly ILogicalServerEndPoint _logicalEndPoint;
        private readonly IMessageRouterFactory _messageRouterFactory;
        private readonly IEndPointManager _endPointManager;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<ClientManager> _logger;

        private readonly Dictionary<EndPointAddress, LinkedListNode<(DateTime leaseEnd, IMessageRouter router, EndPointAddress endPoint)>> _routers;
        private readonly LinkedList<(DateTime leaseEnd, IMessageRouter router, EndPointAddress endPoint)> _sortedRouters;
        private readonly object _routersLock = new object();

        private readonly IAsyncProcess _receiveProcess;
        private readonly IAsyncProcess _garbageCollectionProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public ClientManager(ILogicalServerEndPoint logicalEndPoint,
                             IMessageRouterFactory messageRouterFactory,
                             IEndPointManager endPointManager,
                             IDateTimeProvider dateTimeProvider,
                             ILogger<ClientManager> logger)
        {
            if (logicalEndPoint == null)
                throw new ArgumentNullException(nameof(logicalEndPoint));

            if (messageRouterFactory == null)
                throw new ArgumentNullException(nameof(messageRouterFactory));

            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _logicalEndPoint = logicalEndPoint;
            _messageRouterFactory = messageRouterFactory;
            _endPointManager = endPointManager;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;

            _routers = new Dictionary<EndPointAddress, LinkedListNode<(DateTime leaseEnd, IMessageRouter router, EndPointAddress endPoint)>>();
            _sortedRouters = new LinkedList<(DateTime leaseEnd, IMessageRouter router, EndPointAddress endPoint)>();

            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _garbageCollectionProcess = new AsyncProcess(GarbageCollectionProcess);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region Encoding/Decoding

        private static async Task EncodeRouteResponseAsync(IMessage message, IReadOnlyCollection<IMessage> routeResponse, CancellationToken cancellation)
        {
            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(routeResponse.Count);

                foreach (var response in routeResponse)
                {
                    response.Trim();

                    writer.Write(response.Length);
                    await response.WriteAsync(stream, cancellation);
                }
            }
        }

        private static void EncodeHandleRequest(IMessage message, string route, bool publish)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.Handle);
                writer.Write((short)0); // Padding
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
                writer.Write(publish);
            }
        }

        #endregion

        #region Routers

        private IMessageRouter GetRouter(EndPointAddress endPoint)
        {
            var now = _dateTimeProvider.GetCurrentTime();
            var leaseEnd = now + _leaseLength;

            lock (_routersLock)
            {
                if (_sortedRouters.Count > 0)
                {
                    var firstEntryLeaseEnd = _sortedRouters.First.Value.leaseEnd;

                    if (firstEntryLeaseEnd > leaseEnd)
                    {
                        leaseEnd = _sortedRouters.First.Value.leaseEnd;
                    }
                }

                if (!_routers.TryGetValue(endPoint, out var entry))
                {
                    // TODO: This will hold on the lock while creating the router.
                    var router = CreateRouter(endPoint);
                    entry = _sortedRouters.AddFirst((leaseEnd, router, endPoint));
                    _routers.Add(endPoint, entry);
                }
                else if (leaseEnd > entry.Value.leaseEnd)
                {
                    entry.Value = (leaseEnd, entry.Value.router, entry.Value.endPoint);
                    _sortedRouters.Remove(entry);
                    _sortedRouters.AddFirst(entry);
                }

                return entry.Value.router;
            }
        }

        private IMessageRouter CreateRouter(EndPointAddress endPoint)
        {
            // TODO: Get the clients route options and combine them with RouteOptions.PublishOnly. 
            //       This is not necessary for now as there are no other options currently.
            var messageRouter = _messageRouterFactory.CreateMessageRouter(endPoint,
                                                                          new SerializedMessageHandlerProxy(this, endPoint),
                                                                          RouteOptions.PublishOnly); // We allow publishing only.
            return messageRouter;
        }

        private sealed class SerializedMessageHandlerProxy : ISerializedMessageHandler
        {
            private readonly ClientManager _owner;
            private readonly EndPointAddress _endPoint;

            public SerializedMessageHandlerProxy(ClientManager owner, EndPointAddress endPoint)
            {
                Assert(owner != null);
                Assert(endPoint != null);
                _owner = owner;
                _endPoint = endPoint;
            }

            public async ValueTask<IMessage> HandleAsync(string route, IMessage serializedMessage, bool publish, CancellationToken cancellation = default)
            {
                var frameIdx = serializedMessage.FrameIndex;

                try
                {
                    var message = new Message();

                    do
                    {
                        using (var readStream = serializedMessage.PopFrame().OpenStream())
                        using (var writeStream = message.PushFrame().OpenStream())
                        {
                            readStream.CopyTo(writeStream);
                        }
                    }
                    while (serializedMessage.FrameIndex > -1);

                    EncodeHandleRequest(message, route, publish);

                    var response = await _owner._logicalEndPoint.SendAsync(message, _endPoint, cancellation);
                    return response;
                }
                finally
                {
                    Assert(serializedMessage.FrameIndex <= frameIdx);

                    while (serializedMessage.FrameIndex < frameIdx)
                    {
                        serializedMessage.PushFrame();
                    }
                }
            }
        }

        private async Task GarbageCollectionProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var timeToWait = PerformGarbageCollection();

                    await Task.Delay(timeToWait, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log
                }
            }
        }

        private TimeSpan PerformGarbageCollection()
        {
            var now = _dateTimeProvider.GetCurrentTime();

            LinkedListNode<(DateTime leaseEnd, IMessageRouter router, EndPointAddress endPoint)> node;
            DateTime leaseEnd;

            lock (_routersLock)
            {
                if (_sortedRouters.Count == 0)
                {
                    return TimeSpan.FromSeconds(30);
                }

                node = _sortedRouters.Last;
                leaseEnd = node.Value.leaseEnd;
            }

            while (leaseEnd <= now)
            {
                lock (_routersLock)
                {
                    if (_sortedRouters.Last == node &&
                       node.Value.leaseEnd == leaseEnd)
                    {
                        _sortedRouters.RemoveLast();
                        _routers.Remove(node.Value.endPoint);

                        var router = node.Value.router;
                        router.Dispose();
                    }

                    if (_sortedRouters.Count == 0)
                    {
                        return TimeSpan.FromSeconds(30);
                    }

                    node = _sortedRouters.Last;
                    leaseEnd = node.Value.leaseEnd;
                }
            }

            return leaseEnd - now;
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var receiveResult = await _logicalEndPoint.ReceiveAsync(cancellation);

                    async Task HandleResult()
                    {
                        cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation, receiveResult.Cancellation).Token;

                        try
                        {
                            var response = await ReceiveAsync(receiveResult.Message, receiveResult.RemoteEndPoint, cancellation);

                            if (response != null)
                            {
                                await receiveResult.SendResponseAsync(response);
                            }
                            else
                            {
                                await receiveResult.SendAckAsync();
                            }
                        }
                        catch (OperationCanceledException) when (receiveResult.Cancellation.IsCancellationRequested)
                        {
                            await receiveResult.SendCancellationAsync();
                        }
                    }

                    HandleResult().HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log
                }
            }
        }

        private async Task<IMessage> ReceiveAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var messageType = (MessageType)reader.ReadInt16();
                reader.ReadInt16();

                var router = GetRouter(remoteEndPoint);

                switch (messageType)
                {
                    // TODO: If a message cannot be routed, there seems to be no answer to the client sent.
                    case MessageType.Route:
                        {
                            var routesCount = reader.ReadInt32();
                            var routes = new string[routesCount];
                            for (var i = 0; i < routesCount; i++)
                            {
                                var routeBytesLength = reader.ReadInt32();
                                var routeBytes = reader.ReadBytes(routeBytesLength);
                                routes[i] = Encoding.UTF8.GetString(routeBytes);
                            }

                            var publish = reader.ReadBoolean();
                            var routeResponse = await router.RouteAsync(routes, message, publish, cancellation);
                            var response = new Message();
                            await EncodeRouteResponseAsync(response, routeResponse, cancellation);
                            return response;
                        }

                    case MessageType.RouteToEndPoint:
                        {
                            var routeBytesLength = reader.ReadInt32();
                            var routeBytes = reader.ReadBytes(routeBytesLength);
                            var route = Encoding.UTF8.GetString(routeBytes);
                            var endPointBytesLength = reader.ReadInt32();
                            var endPointBytes = reader.ReadBytes(endPointBytesLength);
                            var endPoint = EndPointAddress.Create(Encoding.UTF8.GetString(endPointBytes));
                            var publish = reader.ReadBoolean();
                            var response = await router.RouteAsync(route, message, publish, endPoint, cancellation);
                            return response;
                        }

                    case MessageType.RegisterRoute:
                    case MessageType.UnregisterRoute:
                        {
                            var routeBytesLength = reader.ReadInt32();
                            var routeBytes = reader.ReadBytes(routeBytesLength);
                            var route = Encoding.UTF8.GetString(routeBytes);

                            if (messageType == MessageType.RegisterRoute)
                            {
                                await router.RegisterRouteAsync(route, cancellation);
                            }
                            else
                            {
                                await router.UnregisterRouteAsync(route, cancellation);
                            }

                            return null;
                        }

                    case MessageType.Ping:
                        {
                            return null;
                        }

                    default:
                        {
                            // TODO: Send bad request message
                            // TODO: Log

                            return null;
                        }
                }
            }
        }

        #endregion

        #region Init

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _receiveProcess.StartAsync(cancellation);
            await _garbageCollectionProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _initializationHelper.CancelAsync().HandleExceptionsAsync();
            await _receiveProcess.TerminateAsync().HandleExceptionsAsync();
            await _garbageCollectionProcess.TerminateAsync().HandleExceptionsAsync();
        }

        #endregion

        private enum MessageType : short
        {
            Route = 0,
            RouteToEndPoint = 1,
            RegisterRoute = 2,
            UnregisterRoute = 3,
            Ping = 4,
            Handle = 5
        }
    }
}
