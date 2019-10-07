using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils.Messaging.Primitives;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Messaging.SignalR.Server
{
    // TODO: Rename
    public sealed class ClientManager : IDisposable
    {
        private readonly ISignalRServerEndPoint _serverEndPoint;
        private readonly IMessageRouterFactory _messageRouterFactory;
        private readonly IRoutingSystem _routingSystem;
        private readonly IClientConnectionManager _connectionManager;
        private readonly ILogger<ClientManager> _logger;

        private readonly Dictionary<RouteEndPointAddress, (IMessageRouter router, Task disonnectionTask)> _routers;
        private readonly AsyncLock _routersLock = new AsyncLock();

        private readonly IAsyncProcess _receiveProcess;
        private bool _isDisposed;

        public ClientManager(ISignalRServerEndPoint serverEndPoint,
                             IMessageRouterFactory messageRouterFactory,
                             IRoutingSystem routingSystem,
                             IClientConnectionManager connectionManager,
                             ILogger<ClientManager> logger)
        {
            if (serverEndPoint == null)
                throw new ArgumentNullException(nameof(serverEndPoint));

            if (messageRouterFactory == null)
                throw new ArgumentNullException(nameof(messageRouterFactory));

            if (routingSystem == null)
                throw new ArgumentNullException(nameof(routingSystem));

            if (connectionManager == null)
                throw new ArgumentNullException(nameof(connectionManager));


            _serverEndPoint = serverEndPoint;
            _messageRouterFactory = messageRouterFactory;
            _routingSystem = routingSystem;
            _connectionManager = connectionManager;
            _logger = logger;

            _routers = new Dictionary<RouteEndPointAddress, (IMessageRouter router, Task disonnectionTask)>();
            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
        }

        #region Encoding/Decoding

        private static void EncodeRouteResponse(ref Message message, IReadOnlyCollection<RouteMessage<IDispatchResult>> routeResponse)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            {
                using (var writer = new BinaryWriter(frameStream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(routeResponse.Count); // TODO: Write 7 bit encoded int.
                }

                foreach (var response in routeResponse)
                {
                    Message.WriteToStream(response.Message, frameStream);
                }
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        private static void EncodeHandleRequest(ref Message message, Route route, bool publish, bool isLocalDispatch)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write((short)MessageType.Handle);
                writer.Write((short)0); // Padding
                writer.Write(route.ToString());
                writer.Write(publish);
                writer.Write(isLocalDispatch);
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        #endregion

        #region Routers

        private async ValueTask<IMessageRouter> GetRouterAsync(RouteEndPointAddress endPoint, CancellationToken cancellation)
        {
            using (await _routersLock.LockAsync(cancellation))
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

                var router = await CreateRouterAsync(endPoint, cancellation);

                async Task ClientDisconnectionWithEntryRemoval()
                {
                    try
                    {
                        await disonnectionTask;
                    }
                    finally
                    {
                        using (await _routersLock.LockAsync())
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

        private ValueTask<IMessageRouter> CreateRouterAsync(RouteEndPointAddress endPoint, CancellationToken cancellation)
        {
            return _messageRouterFactory.CreateMessageRouterAsync(
                endPoint,
                new SerializedMessageHandlerProxy(this, endPoint),
                cancellation);
        }

        private sealed class SerializedMessageHandlerProxy : IRouteMessageHandler
        {
            private readonly ClientManager _owner;
            private readonly RouteEndPointAddress _endPoint;

            public SerializedMessageHandlerProxy(ClientManager owner, RouteEndPointAddress endPoint)
            {
                Assert(owner != null);
                Assert(endPoint != default);
                _owner = owner;
                _endPoint = endPoint;
            }

            public async ValueTask<RouteMessageHandleResult> HandleAsync(
                RouteMessage<DispatchDataDictionary> routeMessage,
                Route route,
                bool publish,
                bool isLocalDispatch,
                CancellationToken cancellation = default)
            {

                var message = routeMessage.Message;
                EncodeHandleRequest(ref message, route, publish, isLocalDispatch);

                var sendResult = await _owner._serverEndPoint.SendAsync(new SignalRServerPacket(message, _endPoint), cancellation);
                return new RouteMessageHandleResult(new RouteMessage<IDispatchResult>(sendResult.Message), sendResult.Handled);
            }
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var receiveResult = await _serverEndPoint.ReceiveAsync(cancellation);
                    HandleAsync(receiveResult, cancellation).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch
                {
                    // TODO: Log
                }
            }
        }

        private async ValueTask HandleAsync(MessageReceiveResult<SignalRServerPacket> receiveResult, CancellationToken cancellation)
        {
            using var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, receiveResult.Cancellation);

            cancellation = combinedCancellationSource.Token;

            try
            {
                var (response, handled) = await HandleAsync(receiveResult.Message, receiveResult.Packet.RemoteEndPoint, cancellation);

                if (response != null)
                {
                    await receiveResult.SendResultAsync(new MessageSendResult(response, handled));
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

        private async Task<(Message message, bool handled)> HandleAsync(Message message, RouteEndPointAddress remoteEndPoint, CancellationToken cancellation)
        {
            message = message.PopFrame(out var frame);

            using var frameStream = frame.OpenStream();
            using var reader = new BinaryReader(frameStream);
            var messageType = (MessageType)reader.ReadInt16();
            reader.ReadInt16();

            var router = await GetRouterAsync(remoteEndPoint, cancellation);

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
                        var routeResponse = await router.RouteAsync(new RouteHierarchy(routes), new RouteMessage<DispatchDataDictionary>(message), publish, cancellation);
                        var response = new Message();
                        EncodeRouteResponse(ref response, routeResponse);
                        return (response, true);
                    }

                case MessageType.RouteToEndPoint:
                    {
                        var routeBytesLength = reader.ReadInt32();
                        var routeBytes = reader.ReadBytes(routeBytesLength);
                        var route = Encoding.UTF8.GetString(routeBytes);
                        var endPoint = reader.ReadEndPointAddress();
                        var publish = reader.ReadBoolean();
                        var response = await router.RouteAsync(new Route(route), new RouteMessage<DispatchDataDictionary>(message), publish, endPoint, cancellation);
                        return (response.Message, true);
                    }

                case MessageType.RegisterRoute:
                    {
                        var options = (RouteRegistrationOptions)reader.ReadInt32();
                        var routeBytesLength = reader.ReadInt32();
                        var routeBytes = reader.ReadBytes(routeBytesLength);
                        var route = Encoding.UTF8.GetString(routeBytes);
                        await router.RegisterRouteAsync(new RouteRegistration(new Route(route), options | RouteRegistrationOptions.PublishOnly), cancellation); // We allow publishing only.
                        return (default, true);
                    }

                case MessageType.UnregisterRoute:
                    {
                        var routeBytesLength = reader.ReadInt32();
                        var routeBytes = reader.ReadBytes(routeBytesLength);
                        var route = Encoding.UTF8.GetString(routeBytes);
                        await router.UnregisterRouteAsync(new Route(route), cancellation);
                        return (default, true);
                    }

                case MessageType.UnregisterRoutes:
                    {
                        var removePersistentRoutes = reader.ReadBoolean();
                        await router.UnregisterRoutesAsync(removePersistentRoutes, cancellation);
                        return (default, true);
                    }

                default:
                    {
                        // TODO: Send bad request message
                        // TODO: Log

                        return default;
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
