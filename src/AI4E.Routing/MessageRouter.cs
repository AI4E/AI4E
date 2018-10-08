﻿using System;
using System.Collections.Concurrent;
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

        private readonly ConcurrentDictionary<int, TaskCompletionSource<IMessage>> _responseTable;
        private int _nextSeqNum = 1;

        private readonly IAsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
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

            _responseTable = new ConcurrentDictionary<int, TaskCompletionSource<IMessage>>();

            _receiveProcess = new AsyncProcess(ReceiveProcedure);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
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
            await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);
            await _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger);
            _logicalEndPoint.Dispose();
            await _routeManager.RemoveRoutesAsync(_logicalEndPoint.EndPoint, cancellation: default).HandleExceptionsAsync(_logger);
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
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = await _logicalEndPoint.ReceiveAsync(cancellation);
                    var (seqNum, messageType, publish, route, corr) = DecodeMessage(message);

                    switch (messageType)
                    {
                        case MessageType.Request:
                            Task.Run(() => ProcessRequestAsync(message, seqNum, publish, route, cancellation)).HandleExceptions();
                            break;

                        case MessageType.Response:
                            Task.Run(() => ProcessResponseAsync(message, seqNum, corr, cancellation)).HandleExceptions();
                            break;

                        case MessageType.Cancellation:// TODO: Implement
                        case MessageType.Unknown:
                        default:
                            _logger?.LogWarning($"End-point '{localEndPoint}': Received bad message that is either of an unkown type or could not be deserialized.");
                            break;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"End-point '{localEndPoint}': Exception while processing incoming message.");
                }
            }
        }

        private async Task ProcessRequestAsync(IMessage message, int seqNum, bool publish, string route, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            _logger?.LogDebug($"End-point '{localEndPoint}': Processing request message with seq-num '{seqNum}'.");

            var frameCount = message.FrameCount;

            // We must push the frame that we read when received the message from the logical end point 
            // in order for the message to look exactly the same as when the logical end point delivered the message to us.
            var frameIdx = message.FrameIndex + 1;
            var response = await RouteToLocalAsync(route, message, publish, cancellation);

            var responseSeqNum = GetNextSeqNum();
            EncodeMessage(response, responseSeqNum, MessageType.Response, default, default, seqNum);

            Assert(message.FrameCount == frameCount);
            Assert(message.FrameIndex <= frameIdx);

            while (message.FrameIndex < frameIdx)
            {
                message.PushFrame();
            }

            Assert(message.FrameIndex == frameIdx);

            await _logicalEndPoint.SendAsync(response, message, cancellation);
        }

        private async Task ProcessResponseAsync(IMessage message, int seqNum, int corr, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            _logger?.LogDebug($"End-point '{localEndPoint}': Processing response message for seq-num '{corr}'. [{seqNum}]");

            if (_responseTable.TryRemove(corr, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }

        #endregion

        private async ValueTask<IMessage> RouteToLocalAsync(string route, IMessage serializedMessage, bool publish, CancellationToken cancellation)
        {
            var frameIdx = serializedMessage.FrameIndex;
            var frameCount = serializedMessage.FrameCount;

            var response = await _serializedMessageHandler.HandleAsync(route, serializedMessage, publish, cancellation);

            // Remove all frames from other protocol stacks.
            response.Trim();

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

            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

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
                List<(EndPointAddress endPoint, RouteOptions options)> matches;

                var matchRouteTask = MatchRouteAsync(route, cancellation);
                var matchRouteResult = await MatchRouteAsync(route, cancellation);
                var matchCount = matchRouteResult.Count();

                matches = matchRouteResult.Where(p => !handledEndPoints.Contains(p.endPoint)).ToList();
                _logger?.LogTrace($"Found {matchCount} ({matchRouteResult.Count()} considering handled end-points) matches for route '{route}'.");

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

                            return null;
                        }

                        var resolvedEndPoint = ResolveEndPoint();

                        // There is no (publish only match) for the current route.
                        if (resolvedEndPoint == null)
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

            return (await Task.WhenAll(tasks.Select(p => p.AsTask()))).ToList(); // TODO: Use ValueTaskHelper.WhenAny
        }

        private async ValueTask<IMessage> InternalRouteAsync(string route, IMessage serializedMessage, bool publish, EndPointAddress endPoint, CancellationToken cancellation)
        {
            Assert(endPoint != null);

            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            // This does short-curcuit the dispatch to the remote end-point. 
            // Any possible replicates do not get any chance to receive the message. 
            // => Requests are kept local to the machine.
            if (endPoint == localEndPoint)
            {
                return await RouteToLocalAsync(route, serializedMessage, publish, cancellation);
            }

            var seqNum = GetNextSeqNum();
            var tcs = new TaskCompletionSource<IMessage>();

            while (!_responseTable.TryAdd(seqNum, tcs))
            {
                seqNum = GetNextSeqNum();
            }

            // TODO: Cancellation

            try
            {
                _logger?.LogDebug($"End-point '{localEndPoint}': Dispatching request message with seq-num '{seqNum}' to remote end point '{endPoint.LogicalAddress}'.");

                // Remove all frames from other protocol stacks.
                serializedMessage.Trim();

                // We do not want to override frames.
                Assert(serializedMessage.FrameIndex == serializedMessage.FrameCount - 1);

                EncodeMessage(serializedMessage, seqNum, MessageType.Request, publish, route, default);

                await _logicalEndPoint.SendAsync(serializedMessage, endPoint, cancellation);

                var response = await tcs.Task;

                response.Trim();

                return response;
            }
            finally
            {
                _responseTable.TryRemove(seqNum, tcs);
            }
        }

        private static void EncodeMessage(IMessage message, int seqNum, MessageType messageType, bool publish, string route, int corr)
        {
            var routeBytes = route != null ? Encoding.UTF8.GetBytes(route) : _emptyBytes;

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(seqNum); // 4 Byte
                writer.Write((short)messageType); // 2 Byte

                if (messageType == MessageType.Request)
                {
                    writer.Write((byte)0); // 1 Byte (padding)
                    writer.Write(publish); // 1 Byte
                    writer.Write(routeBytes.Length); // 4 Bytes

                    if (routeBytes.Length > 0)
                    {
                        writer.Write(routeBytes); // Variable length
                    }
                }
                else if (messageType == MessageType.Response || messageType == MessageType.Cancellation)
                {
                    writer.Write((short)0); // 2 Bytes (padding)
                    writer.Write(corr);
                }
            }
        }

        private static (int seqNum, MessageType messageType, bool publish, string route, int corr) DecodeMessage(IMessage message)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var seqNum = reader.ReadInt32(); // 4 Byte
                var messageType = (MessageType)reader.ReadInt16(); // 2 Byte

                var publish = false;
                var route = default(string);
                var corr = 0;

                if (messageType == MessageType.Request)
                {
                    reader.ReadByte(); // 1 Byte (padding)
                    publish = reader.ReadBoolean();  // 1 Byte

                    var routeBytesLength = reader.ReadInt32(); // 4 Byte
                    if (routeBytesLength > 0)
                    {
                        var routeBytes = reader.ReadBytes(routeBytesLength); // Variable length
                        route = Encoding.UTF8.GetString(routeBytes);
                    }
                }
                else if (messageType == MessageType.Response || messageType == MessageType.Cancellation)
                {
                    reader.ReadInt16(); // 2 Byte (padding)

                    corr = reader.ReadInt32(); // 4 Byte
                }

                return (seqNum, messageType, publish, route, corr);
            }
        }

        private enum MessageType : short
        {
            Unknown = 0,
            Request = 1,
            Response = 2,
            Cancellation = 3
        }

        public async Task RegisterRouteAsync(string route, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            await _routeManager.AddRouteAsync(localEndPoint, route, cancellation);
        }

        public async Task UnregisterRouteAsync(string route, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation);

            await _routeManager.RemoveRouteAsync(localEndPoint, route, cancellation);
        }

        private Task<IEnumerable<(EndPointAddress endPoint, RouteOptions options)>> MatchRouteAsync(string route, CancellationToken cancellation)
        {
            return _routeManager.GetRoutesAsync(route, cancellation);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
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
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

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
