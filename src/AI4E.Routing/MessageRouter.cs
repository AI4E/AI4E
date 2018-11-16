using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class MessageRouter : IMessageRouter, IAsyncDisposable
    {
        private static readonly byte[] _emptyBytes = new byte[0];

        private readonly ISerializedMessageHandler _serializedMessageHandler;
        private readonly ILogicalEndPoint _logicalEndPoint;
        private readonly IRouteManager _routeManager;
        private readonly ILogger<MessageRouter> _logger;

        private readonly IAsyncProcess _receiveProcess;
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
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<EndPointAddress>(_logicalEndPoint.EndPoint);
        }

        #region Initialization

        private Task InitializeInternalAsync(CancellationToken cancellation)
        {
            return _receiveProcess.StartAsync();
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        private async Task DisposeInternalAsync()
        {
            // Cancel the initialization
            await _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger);
            _logicalEndPoint.Dispose();
            await _routeManager.RemoveRoutesAsync(_logicalEndPoint.EndPoint, removePersistentRoutes: false, cancellation: default).HandleExceptionsAsync(_logger);
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
            // We cache the delegate for perf reasons.
            var handler = new Func<IMessage, EndPointAddress, CancellationToken, Task<IMessage>>(HandleAsync);
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

        private async Task<IMessage> HandleAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);
            var (publish, route) = DecodeMessage(message);

            _logger?.LogDebug($"End-point '{localEndPoint}': Processing request message.");
            var response = await RouteToLocalAsync(route, message, publish, cancellation);

            // TODO: Do we want to send a message frame back to the initiator? This would be empty for now.

            return response;
        }

        #endregion

        private async ValueTask<IMessage> RouteToLocalAsync(string route, IMessage serializedMessage, bool publish, CancellationToken cancellation)
        {
            var frameIdx = serializedMessage.FrameIndex;
            var frameCount = serializedMessage.FrameCount;

            var response = await _serializedMessageHandler.HandleAsync(route, serializedMessage, publish, cancellation);

            // Remove all frames from other protocol stacks.
            response.Trim(); // TODO

            // We do not want to override frames.
            Assert(response.FrameIndex == response.FrameCount - 1);
            Assert(frameIdx == serializedMessage.FrameIndex);
            Assert(frameCount == serializedMessage.FrameCount);

            return response;
        }

        public ValueTask<IMessage> RouteAsync(string route, IMessage serializedMessage, bool publish, EndPointAddress endPoint, CancellationToken cancellation)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            return InternalRouteAsync(route, serializedMessage, publish, endPoint, cancellation);
        }

        public ValueTask<IReadOnlyCollection<IMessage>> RouteAsync(IEnumerable<string> routes, IMessage serializedMessage, bool publish, CancellationToken cancellation)
        {
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            if (!routes.Any())
                throw new ArgumentException("The collection must not be empty.", nameof(routes));

            if (routes.Any(p => p == null))
                throw new ArgumentException("The collection must not contain null values.", nameof(routes));

            return InternalRouteAsync(routes, serializedMessage, publish, cancellation);
        }

        private async ValueTask<IReadOnlyCollection<IMessage>> InternalRouteAsync(IEnumerable<string> routes, IMessage serializedMessage, bool publish, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);
            var tasks = new List<ValueTask<IMessage>>();
            var handledEndPoints = new HashSet<EndPointAddress>();

            _logger?.LogTrace($"Routing a message ({(publish ? "publish" : "p2p")}) with routes: {routes.Aggregate((e, n) => e + ", " + n)}");

            foreach (var route in routes)
            {
                var matchRouteResult = await MatchRouteAsync(route, cancellation);
                var matches = matchRouteResult.Where(p => !handledEndPoints.Contains(p.endPoint)).ToList();

                _logger?.LogTrace($"Found {matchRouteResult.Count()} ({matches.Count()} considering handled end-points) matches for route '{route}'.");

                var endPoints = matches.Select(p => p.endPoint);
                handledEndPoints.UnionWith(endPoints);

                if (matches.Any())
                {
                    if (!publish)
                    {
                        EndPointAddress ResolveEndPoint()
                        {
                            for (var i = matches.Count - 1; i >= 0; i--)
                            {
                                var (endPoint, options) = matches[i];

                                if ((options & RouteOptions.PublishOnly) != RouteOptions.PublishOnly)
                                {
                                    return endPoint;
                                }
                            }

                            return EndPointAddress.UnknownAddress;
                        }

                        var resolvedEndPoint = ResolveEndPoint();

                        // There is no (publish only) match for the current route.
                        if (resolvedEndPoint == EndPointAddress.UnknownAddress)
                        {
                            continue;
                        }

                        if (resolvedEndPoint.Equals(localEndPoint))
                        {
                            return new[] { await RouteToLocalAsync(route, serializedMessage, publish: false, cancellation) };
                        }

                        return new[] { await InternalRouteAsync(route, serializedMessage, publish: false, resolvedEndPoint, cancellation) };
                    }

                    foreach (var (endPoint, _) in matches)
                    {
                        if (endPoint.Equals(localEndPoint))
                        {
                            _logger?.LogTrace("Routing to end-point: " + endPoint + " (local end-point)");

                            tasks.Add(RouteToLocalAsync(route, serializedMessage, publish: true, cancellation));
                        }
                        else
                        {
                            _logger?.LogTrace("Routing to end-point: " + endPoint);

                            tasks.Add(InternalRouteAsync(route, serializedMessage, publish: true, endPoint, cancellation));
                        }
                    }
                }
            }

            var result = (await Task.WhenAll(tasks.Select(p => p.AsTask()))).ToList(); // TODO: Use ValueTaskHelper.WhenAny

            _logger?.LogTrace($"Successfully routed a message ({(publish ? "publish" : "p2p")}) with routes: {routes.Aggregate((e, n) => e + ", " + n)}");

            return result;
        }

        private async ValueTask<IMessage> InternalRouteAsync(string route, IMessage serializedMessage, bool publish, EndPointAddress endPoint, CancellationToken cancellation)
        {
            Assert(endPoint != default);

            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            // This does short-curcuit the dispatch to the remote end-point. 
            // Any possible replicates do not get any chance to receive the message. 
            // => Requests are kept local to the machine.
            if (endPoint == localEndPoint)
            {
                return await RouteToLocalAsync(route, serializedMessage, publish, cancellation);
            }

            _logger?.LogDebug($"Message router for end-point '{localEndPoint}': Dispatching request message to remote end point '{endPoint}'.");

            // Remove all frames from other protocol stacks.
            serializedMessage.Trim(); // TODO

            // We do not want to override frames.
            Assert(serializedMessage.FrameIndex == serializedMessage.FrameCount - 1);

            EncodeMessage(serializedMessage, publish, route);

            var response = await _logicalEndPoint.SendAsync(serializedMessage, endPoint, cancellation);

            _logger?.LogDebug($"Message router for end-point '{localEndPoint}': Processing response message."); // TODO

            response.Trim(); // TODO

            return response;
        }

        private static void EncodeMessage(IMessage message, bool publish, string route)
        {
            var routeBytes = route != null ? Encoding.UTF8.GetBytes(route) : _emptyBytes;

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(publish);              // 1 Byte
                writer.Write((short)0);             // 2 Byte (padding)
                writer.Write((byte)0);              // 1 Byte (padding)
                writer.Write(routeBytes.Length);    // 4 Bytes

                if (routeBytes.Length > 0)
                {
                    writer.Write(routeBytes);       // Variable length
                }
            }
        }

        private static (bool publish, string route) DecodeMessage(IMessage message)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var publish = false;
                var route = default(string);

                publish = reader.ReadBoolean();                             // 1 Byte
                reader.ReadInt16();                                         // 2 Byte (padding)
                reader.ReadByte();                                          // 1 Byte (padding)

                var routeBytesLength = reader.ReadInt32();                  // 4 Byte
                if (routeBytesLength > 0)
                {
                    var routeBytes = reader.ReadBytes(routeBytesLength);    // Variable length
                    route = Encoding.UTF8.GetString(routeBytes);
                }

                return (publish, route);
            }
        }

        public async Task RegisterRouteAsync(string route, RouteRegistrationOptions options, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);
            await _routeManager.AddRouteAsync(localEndPoint, route, options, cancellation);
        }

        public async Task UnregisterRouteAsync(string route, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            await _routeManager.RemoveRouteAsync(localEndPoint, route, cancellation);
        }

        public Task UnregisterRoutesAsync(bool removePersistentRoutes, CancellationToken cancellation)
        {
            return _routeManager.RemoveRoutesAsync(_logicalEndPoint.EndPoint, removePersistentRoutes, cancellation);
        }

        private async Task<IEnumerable<(EndPointAddress endPoint, RouteOptions options)>> MatchRouteAsync(string route, CancellationToken cancellation)
        {
            return (await _routeManager.GetRoutesAsync(route, cancellation)).Select(p => (p.endPoint, p.options));
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

        public IMessageRouter CreateMessageRouter(EndPointAddress endPoint, ISerializedMessageHandler serializedMessageHandler, RouteOptions options)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            if (_logicalEndPoint.EndPoint == endPoint)
            {
                return CreateMessageRouterInternal(_logicalEndPoint, serializedMessageHandler, options);
            }

            var logicalEndPoint = _endPointManager.CreateLogicalEndPoint(endPoint);
            return CreateMessageRouterInternal(logicalEndPoint, serializedMessageHandler, options);
        }

        public IMessageRouter CreateMessageRouter(ISerializedMessageHandler serializedMessageHandler, RouteOptions options)
        {
            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            return CreateMessageRouterInternal(_logicalEndPoint, serializedMessageHandler, options);
        }

        private IMessageRouter CreateMessageRouterInternal(ILogicalEndPoint endPoint, ISerializedMessageHandler serializedMessageHandler, RouteOptions options)
        {
            var logger = _loggerFactory?.CreateLogger<MessageRouter>();
            var routeStore = _routeManagerFactory.CreateRouteManager(options);

            Assert(routeStore != null);

            return new MessageRouter(serializedMessageHandler, endPoint, routeStore, logger);
        }
    }
}
