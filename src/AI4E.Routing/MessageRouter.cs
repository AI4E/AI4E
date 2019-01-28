using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class MessageRouter : IMessageRouter
    {
        private readonly ISerializedMessageHandler _serializedMessageHandler;
        private readonly ILogicalEndPoint _logicalEndPoint;
        private readonly IRouteManager _routeManager;
        private readonly ILogger<MessageRouter> _logger;

        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncDisposeHelper _disposeHelper;

        public MessageRouter(ISerializedMessageHandler serializedMessageHandler,
                             ILogicalEndPoint logicalEndPoint,
                             IRouteManager routeManager,
                             ILogger<MessageRouter> logger = null)
        {
            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            if (logicalEndPoint == null)
                throw new ArgumentNullException(nameof(logicalEndPoint));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            _serializedMessageHandler = serializedMessageHandler;
            _logicalEndPoint = logicalEndPoint;
            _routeManager = routeManager;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcedure, start: true);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync, AsyncDisposeHelperOptions.Synchronize);
        }

        public ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<EndPointAddress>(_logicalEndPoint.EndPoint);
        }

        #region Disposal

        private async Task DisposeInternalAsync()
        {
            await _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger);
            _logicalEndPoint.Dispose();
            await _routeManager.RemoveRoutesAsync(_logicalEndPoint.EndPoint, removePersistentRoutes: false, cancellation: default).HandleExceptionsAsync(_logger);
        }

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        #endregion

        #region Receive Process

        private async Task ReceiveProcedure(CancellationToken cancellation)
        {
            // We cache the delegate for perf reasons.
            var handler = new Func<IMessage, EndPointAddress, CancellationToken, Task<(IMessage response, bool handled)>>(HandleAsync);
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var receiveResult = await _logicalEndPoint.ReceiveAsync(cancellation);
                    receiveResult.HandleAsync(handler, cancellation).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"End-point '{localEndPoint}': Exception while processing incoming message.");
                }
            }
        }

        private async Task<(IMessage response, bool handled)> HandleAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);
            var (publish, localDispatch, route) = DecodeMessage(message);

            _logger?.LogDebug($"End-point '{localEndPoint}': Processing request message.");
            return await RouteToLocalAsync(route, message, publish, localDispatch, cancellation);
        }

        #endregion

        private async ValueTask<(IMessage response, bool handled)> RouteToLocalAsync(Route route, IMessage request, bool publish, bool localDispatch, CancellationToken cancellation)
        {
            var frameIdx = request.FrameIndex;
            var frameCount = request.FrameCount;

            var (response, handled) = await _serializedMessageHandler.HandleAsync(route, request, publish, localDispatch, cancellation);

            // Remove all frames from other protocol stacks.
            response.Trim(); // TODO

            // We do not want to override frames.
            Assert(response.FrameIndex == response.FrameCount - 1);
            Assert(frameIdx == request.FrameIndex);
            Assert(frameCount == request.FrameCount);

            return (response, handled);
        }

        public async ValueTask<IMessage> RouteAsync(Route route, IMessage serializedMessage, bool publish, EndPointAddress endPoint, CancellationToken cancellation)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            try
            {
                using (var guard = await _disposeHelper.GuardDisposalAsync(cancellation))
                {
                    var (response, _) = await InternalRouteAsync(route, serializedMessage, publish, endPoint, cancellation);

                    return response;
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async ValueTask<IReadOnlyCollection<IMessage>> RouteAsync(RouteHierarchy routes, IMessage serializedMessage, bool publish, CancellationToken cancellation)
        {
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            if (!routes.Any())
                throw new ArgumentException("The collection must not be empty.", nameof(routes));

            if (routes.Any(p => p == null))
                throw new ArgumentException("The collection must not contain null values.", nameof(routes));

            try
            {
                using (var guard = await _disposeHelper.GuardDisposalAsync(cancellation))
                {
                    return await InternalRouteAsync(routes, serializedMessage, publish, cancellation);
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private async ValueTask<IReadOnlyCollection<IMessage>> InternalRouteAsync(RouteHierarchy routes, IMessage serializedMessage, bool publish, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);
            var tasks = new List<ValueTask<(IMessage response, bool handled)>>();
            var handledEndPoints = new HashSet<EndPointAddress>();

            _logger?.LogTrace($"Routing a message ({(publish ? "publish" : "p2p")}) with routes: {routes}");

            foreach (var route in routes)
            {
                var matches = await MatchRouteAsync(route, publish, handledEndPoints, cancellation);

                if (matches.Any())
                {
                    if (!publish)
                    {
                        _logger?.LogTrace($"Found {matches.Count()} matches for route '{route}'.");

                        for (var i = matches.Count - 1; i >= 0; i--)
                        {
                            var (endPoint, options) = matches[i];

                            if (endPoint == EndPointAddress.UnknownAddress)
                            {
                                continue;
                            }

                            if ((options & RouteRegistrationOptions.PublishOnly) == RouteRegistrationOptions.PublishOnly)
                            {
                                continue;
                            }

                            var (response, handled) = await InternalRouteAsync(route, serializedMessage, publish: false, endPoint, cancellation);

                            if (handled)
                            {
                                return response.Yield().ToArray();
                            }
                        }
                    }
                    else
                    {
                        _logger?.LogTrace($"Found {matches.Count()} matches (considering handled end-points) for route '{route}'.");

                        var endPoints = matches.Select(p => p.EndPoint);
                        handledEndPoints.UnionWith(endPoints);
                        tasks.AddRange(endPoints.Select(endPoint => InternalRouteAsync(route, serializedMessage, publish: true, endPoint, cancellation)));
                    }
                }
            }

            var result = await ValueTaskHelper.WhenAll(tasks, preserveOrder: false);

            _logger?.LogTrace($"Successfully routed a message ({(publish ? "publish" : "p2p")}) with routes: {routes}");

            return result.Where(p => p.handled).Select(p => p.response).ToArray();
        }

        private async Task<List<RouteTarget>> MatchRouteAsync(
            Route route,
            bool publish,
            ISet<EndPointAddress> handledEndPoints,
            CancellationToken cancellation)
        {
            var routeResults = await _routeManager.GetRoutesAsync(route, cancellation);

            if (publish)
            {
                routeResults = routeResults.Where(p => !handledEndPoints.Contains(p.EndPoint));
            }

            var localEndPoint = await GetLocalEndPointAsync(cancellation);
            routeResults = routeResults.Where(p => localEndPoint == p.EndPoint || !p.RegistrationOptions.IncludesFlag(RouteRegistrationOptions.LocalDispatchOnly));
            return routeResults.ToList();
        }

        private async ValueTask<(IMessage response, bool handled)> InternalRouteAsync(Route route, IMessage serializedMessage, bool publish, EndPointAddress endPoint, CancellationToken cancellation)
        {
            Assert(endPoint != default);

            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            // This does short-circuit the dispatch to the remote end-point. 
            // Any possible replicates do not get any chance to receive the message. 
            // => Requests are kept local to the machine.
            if (endPoint == localEndPoint)
            {
                _logger?.LogDebug($"Message router for end-point '{localEndPoint}': Dispatching request message locally.");

                return await RouteToLocalAsync(route, serializedMessage, publish, localDispatch: true, cancellation);
            }

            _logger?.LogDebug($"Message router for end-point '{localEndPoint}': Dispatching request message to remote end point '{endPoint}'.");

            // Remove all frames from other protocol stacks.
            serializedMessage.Trim(); // TODO

            // We do not want to override frames.
            Assert(serializedMessage.FrameIndex == serializedMessage.FrameCount - 1);

            EncodeMessage(serializedMessage, publish, localDispatch: false, route);

            var (response, handled) = await _logicalEndPoint.SendAsync(serializedMessage, endPoint, cancellation);

            _logger?.LogDebug($"Message router for end-point '{localEndPoint}': Processing response message."); // TODO

            response.Trim(); // TODO

            return (response, handled);
        }

        private static void EncodeMessage(IMessage message, bool publish, bool localDispatch, Route route)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route.ToString());

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(publish);              // 1 Byte
                writer.Write(localDispatch);        // 1 Byte
                writer.Write((short)0);             // 2 Byte (padding)
                writer.Write(routeBytes.Length);    // 4 Bytes

                if (routeBytes.Length > 0)
                {
                    writer.Write(routeBytes);       // Variable length
                }
            }
        }

        private static (bool publish, bool localDispatch, Route route) DecodeMessage(IMessage message)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var route = default(Route);

                var publish = reader.ReadBoolean();                         // 1 Byte
                var localDispatch = reader.ReadBoolean();                   // 1 Byte
                reader.ReadInt16();                                         // 2 Byte (padding)

                var routeBytesLength = reader.ReadInt32();                  // 4 Byte
                if (routeBytesLength > 0)
                {
                    var routeBytes = reader.ReadBytes(routeBytesLength);    // Variable length
                    route = new Route(Encoding.UTF8.GetString(routeBytes));
                }

                return (publish, localDispatch, route);
            }
        }

        public async Task RegisterRouteAsync(RouteRegistration routeRegistration, CancellationToken cancellation)
        {
            try
            {
                using (var guard = await _disposeHelper.GuardDisposalAsync(cancellation))
                {
                    var localEndPoint = await GetLocalEndPointAsync(cancellation);
                    await _routeManager.AddRouteAsync(localEndPoint, routeRegistration, cancellation);
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task UnregisterRouteAsync(Route route, CancellationToken cancellation)
        {
            try
            {
                using (var guard = await _disposeHelper.GuardDisposalAsync(cancellation))
                {
                    var localEndPoint = await GetLocalEndPointAsync(cancellation);
                    await _routeManager.RemoveRouteAsync(localEndPoint, route, cancellation);
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task UnregisterRoutesAsync(bool removePersistentRoutes, CancellationToken cancellation)
        {
            try
            {
                using (var guard = await _disposeHelper.GuardDisposalAsync(cancellation))
                {
                    await _routeManager.RemoveRoutesAsync(_logicalEndPoint.EndPoint, removePersistentRoutes, cancellation);
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }

    public sealed class MessageRouterFactory : IMessageRouterFactory
    {
        private readonly IRouteManagerFactory _routeManagerFactory;
        private readonly IEndPointManager _endPointManager;
        private readonly ILogicalEndPoint _logicalEndPoint;
        private readonly ILoggerFactory _loggerFactory;

        public MessageRouterFactory(IRouteManagerFactory routeManagerFactory,
                                    IEndPointManager endPointManager,
                                    ILogicalEndPoint logicalEndPoint,
                                    ILoggerFactory loggerFactory = null)
        {
            if (routeManagerFactory == null)
                throw new ArgumentNullException(nameof(routeManagerFactory));

            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (logicalEndPoint == null)
                throw new ArgumentNullException(nameof(logicalEndPoint));

            _routeManagerFactory = routeManagerFactory;
            _endPointManager = endPointManager;
            _logicalEndPoint = logicalEndPoint;
            _loggerFactory = loggerFactory;
        }

        public ValueTask<EndPointAddress> GetDefaultEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<EndPointAddress>(_logicalEndPoint.EndPoint);
        }

        public IMessageRouter CreateMessageRouter(EndPointAddress endPoint, ISerializedMessageHandler serializedMessageHandler)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            if (_logicalEndPoint.EndPoint == endPoint)
            {
                return CreateMessageRouterInternal(_logicalEndPoint, serializedMessageHandler);
            }

            var logicalEndPoint = _endPointManager.CreateLogicalEndPoint(endPoint);
            return CreateMessageRouterInternal(logicalEndPoint, serializedMessageHandler);
        }

        public IMessageRouter CreateMessageRouter(ISerializedMessageHandler serializedMessageHandler)
        {
            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            return CreateMessageRouterInternal(_logicalEndPoint, serializedMessageHandler);
        }

        private IMessageRouter CreateMessageRouterInternal(ILogicalEndPoint endPoint, ISerializedMessageHandler serializedMessageHandler)
        {
            var logger = _loggerFactory?.CreateLogger<MessageRouter>();
            var routeStore = _routeManagerFactory.CreateRouteManager();

            Assert(routeStore != null);

            return new MessageRouter(serializedMessageHandler, endPoint, routeStore, logger);
        }
    }
}
