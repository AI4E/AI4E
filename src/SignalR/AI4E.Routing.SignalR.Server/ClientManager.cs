using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Utils;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    // TODO: Rename
    public sealed class ClientManager : IDisposable
    {
        private readonly IRequestReplyServerEndPoint _endPoint;
        private readonly IMessageRouterFactory _messageRouterFactory;
        private readonly IEndPointManager _endPointManager;
        private readonly IClientConnectionManager _connectionManager;
        private readonly ILogger<ClientManager> _logger;

        private readonly Dictionary<EndPointAddress, (IMessageRouter router, Task disonnectionTask)> _routers;
        private readonly object _routersLock = new object();

        private readonly IAsyncProcess _receiveProcess;
        private bool _isDisposed;

        public ClientManager(IRequestReplyServerEndPoint endPoint,
                             IMessageRouterFactory messageRouterFactory,
                             IEndPointManager endPointManager,
                             IClientConnectionManager connectionManager,
                             ILogger<ClientManager> logger)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (messageRouterFactory == null)
                throw new ArgumentNullException(nameof(messageRouterFactory));

            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (connectionManager == null)
                throw new ArgumentNullException(nameof(connectionManager));


            _endPoint = endPoint;
            _messageRouterFactory = messageRouterFactory;
            _endPointManager = endPointManager;
            _connectionManager = connectionManager;
            _logger = logger;

            _routers = new Dictionary<EndPointAddress, (IMessageRouter router, Task disonnectionTask)>();
            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
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

        private static void EncodeHandleRequest(IMessage message, Route route, bool publish, bool isLocalDispatch)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route.ToString());

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.Handle);
                writer.Write((short)0); // Padding
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
                writer.Write(publish);
                writer.Write(isLocalDispatch);
            }
        }

        #endregion

        #region Routers

        private IMessageRouter GetRouter(EndPointAddress endPoint)
        {
            lock (_routersLock)
            {
                if (_routers.TryGetValue(endPoint, out var entry))
                {
                    return entry.router;
                }

                var disonnectionTask = _connectionManager.WaitForDisconnectAsync(endPoint, cancellation: default);

                if (disonnectionTask.IsCompleted)
                {
                    return null;
                }

                var router = CreateRouter(endPoint);

                async Task ClientDisconnectionWithEntryRemoval()
                {
                    try
                    {
                        await disonnectionTask;
                    }
                    finally
                    {
                        lock (_routersLock)
                        {
                            _routers.Remove(endPoint);
                        }

                        await router.UnregisterRoutesAsync(removePersistentRoutes: true);
                        router.Dispose();
                    }
                }

                _routers.Add(endPoint, (router, ClientDisconnectionWithEntryRemoval()));

                return router;
            }
        }

        private IMessageRouter CreateRouter(EndPointAddress endPoint)
        {
            var messageRouter = _messageRouterFactory.CreateMessageRouter(endPoint, new SerializedMessageHandlerProxy(this, endPoint));
            return messageRouter;
        }

        private sealed class SerializedMessageHandlerProxy : ISerializedMessageHandler
        {
            private readonly ClientManager _owner;
            private readonly EndPointAddress _endPoint;

            public SerializedMessageHandlerProxy(ClientManager owner, EndPointAddress endPoint)
            {
                Assert(owner != null);
                Assert(endPoint != default);
                _owner = owner;
                _endPoint = endPoint;
            }

            public async ValueTask<(IMessage response, bool handled)> HandleAsync(Route route, IMessage request, bool publish, bool isLocalDispatch, CancellationToken cancellation = default)
            {
                var frameIdx = request.FrameIndex;

                try
                {
                    var message = new Message();

                    do
                    {
                        using (var readStream = request.PopFrame().OpenStream())
                        using (var writeStream = message.PushFrame().OpenStream())
                        {
                            readStream.CopyTo(writeStream);
                        }
                    }
                    while (request.FrameIndex > -1);

                    EncodeHandleRequest(message, route, publish, isLocalDispatch);

                    var (response, handled) = await _owner._endPoint.SendAsync(message, _endPoint, cancellation);
                    return (response, handled);
                }
                finally
                {
                    Assert(request.FrameIndex <= frameIdx);

                    while (request.FrameIndex < frameIdx)
                    {
                        request.PushFrame();
                    }
                }
            }
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            // We cache the delegate for perf reasons.
            var handler = new Func<IMessage, EndPointAddress, CancellationToken, Task<IMessage>>(HandleAsync);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var receiveResult = await _endPoint.ReceiveAsync(cancellation);
                    receiveResult.HandleAsync(handler, cancellation).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log
                }
            }
        }

        private async Task<IMessage> HandleAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var messageType = (MessageType)reader.ReadInt16();
                reader.ReadInt16();

                var router = GetRouter(remoteEndPoint);

                switch (messageType)
                {
                    case MessageType.Route:
                        {
                            var routesCount = reader.ReadInt32();
                            var routes = new Route[routesCount];
                            for (var i = 0; i < routesCount; i++)
                            {
                                var routeBytesLength = reader.ReadInt32();
                                var routeBytes = reader.ReadBytes(routeBytesLength);
                                routes[i] = new Route(Encoding.UTF8.GetString(routeBytes));
                            }

                            var publish = reader.ReadBoolean();
                            var routeResponse = await router.RouteAsync(new RouteHierarchy(routes), message, publish, cancellation);
                            var response = new Message();
                            await EncodeRouteResponseAsync(response, routeResponse, cancellation);
                            return response;
                        }

                    case MessageType.RouteToEndPoint:
                        {
                            var routeBytesLength = reader.ReadInt32();
                            var routeBytes = reader.ReadBytes(routeBytesLength);
                            var route = Encoding.UTF8.GetString(routeBytes);
                            var endPoint = reader.ReadEndPointAddress();
                            var publish = reader.ReadBoolean();
                            var response = await router.RouteAsync(new Route(route), message, publish, endPoint, cancellation);
                            return response;
                        }

                    case MessageType.RegisterRoute:
                        {
                            var options = (RouteRegistrationOptions)reader.ReadInt32();
                            var routeBytesLength = reader.ReadInt32();
                            var routeBytes = reader.ReadBytes(routeBytesLength);
                            var route = Encoding.UTF8.GetString(routeBytes);
                            await router.RegisterRouteAsync(new RouteRegistration(new Route(route), options | RouteRegistrationOptions.PublishOnly), cancellation); // We allow publishing only.
                            return null;
                        }

                    case MessageType.UnregisterRoute:
                        {
                            var routeBytesLength = reader.ReadInt32();
                            var routeBytes = reader.ReadBytes(routeBytesLength);
                            var route = Encoding.UTF8.GetString(routeBytes);
                            await router.UnregisterRouteAsync(new Route(route), cancellation);

                            return null;
                        }

                    case MessageType.UnregisterRoutes:
                        {
                            var removePersistentRoutes = reader.ReadBoolean();
                            await router.UnregisterRoutesAsync(removePersistentRoutes, cancellation);
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

        #region Disposal

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                _receiveProcess.Terminate();
            }
        }
        #endregion

        private enum MessageType : short
        {
            Route = 0,
            RouteToEndPoint = 1,
            RegisterRoute = 2,
            UnregisterRoute = 3,
            UnregisterRoutes = 4,
            Handle = 5
        }
    }
}
