using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Utils;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    // TODO: Logging   
    public sealed class RemoteMessageRouter : IMessageRouter
    {
        private readonly ISerializedMessageHandler _serializedMessageHandler;
        private readonly IRequestReplyClientEndPoint _logicalEndPoint;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<RemoteMessageRouter> _logger;

        private readonly IAsyncProcess _receiveProcess;
        private volatile CancellationTokenSource _disposalSource = new CancellationTokenSource();

        public RemoteMessageRouter(ISerializedMessageHandler serializedMessageHandler,
                                   IRequestReplyClientEndPoint logicalEndPoint,
                                   IDateTimeProvider dateTimeProvider,
                                   ILogger<RemoteMessageRouter> logger = null)
        {
            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            if (logicalEndPoint == null)
                throw new ArgumentNullException(nameof(logicalEndPoint));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _serializedMessageHandler = serializedMessageHandler;
            _logicalEndPoint = logicalEndPoint;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
        }

        #region IMessageRouter

        public ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return _logicalEndPoint.GetLocalEndPointAsync(cancellation);
        }

        public async ValueTask<IReadOnlyCollection<IMessage>> RouteAsync(
            RouteHierarchy routes,
            IMessage serializedMessage,
            bool publish,
            CancellationToken cancellation)
        {
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));

            if (routes.Any(p => p == null))
                throw new ArgumentException("The collection must not contain null values.", nameof(routes));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            var idx = serializedMessage.FrameIndex;

            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    try
                    {
                        EncodeRouteRequest(serializedMessage, routes, publish);

                        var response = await SendInternalAsync(serializedMessage, cancellation);
                        var result = await DecodeRouteResponseAsync(response, cancellation);

                        return result;
                    }
                    catch
                    {
                        Assert(serializedMessage.FrameIndex >= idx);

                        while (serializedMessage.FrameIndex > idx)
                        {
                            serializedMessage.PopFrame();
                        }

                        throw;
                    }
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async ValueTask<IMessage> RouteAsync(
            Route route,
            IMessage serializedMessage,
            bool publish,
            EndPointAddress endPoint,
            CancellationToken cancellation)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            var idx = serializedMessage.FrameIndex;

            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    try
                    {
                        EncodeRouteRequest(serializedMessage, route, publish, endPoint);

                        var response = await SendInternalAsync(serializedMessage, cancellation);

                        return response;
                    }
                    catch
                    {
                        Assert(serializedMessage.FrameIndex >= idx);

                        while (serializedMessage.FrameIndex > idx)
                        {
                            serializedMessage.PopFrame();
                        }

                        throw;
                    }
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public Task RegisterRouteAsync(
            Route route,
            RouteRegistrationOptions options,
            CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = new Message();
                    EncodeRegisterRouteRequest(message, route, options);
                    return SendInternalAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public Task UnregisterRouteAsync(
            Route route,
            CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = new Message();
                    EncodeUnregisterRouteRequest(message, route);
                    return SendInternalAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public Task UnregisterRoutesAsync(bool removePersistentRoutes, CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = new Message();
                    EncodeUnregisterRouteRequest(message, removePersistentRoutes);
                    return SendInternalAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        #endregion

        #region Encoding/Decoding

        private static async ValueTask<IReadOnlyCollection<IMessage>> DecodeRouteResponseAsync(IMessage message, CancellationToken cancellation)
        {
            IMessage[] result;

            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var resultCount = reader.ReadInt32();

                result = new IMessage[resultCount];

                for (var i = 0; i < resultCount; i++)
                {
                    var messageLength = reader.ReadInt64();
                    var resultMessage = new Message();

                    using (var messageStream = new MemoryStream(reader.ReadBytes(checked((int)messageLength))))
                    {
                        messageStream.Position = 0;
                        await resultMessage.ReadAsync(messageStream, cancellation);
                    }

                    result[i] = resultMessage;
                }
            }

            return result;
        }

        private static void EncodeRouteRequest(IMessage message, RouteHierarchy routes, bool publish)
        {
            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.Route);
                writer.Write((short)0); // Padding
                writer.Write(routes.Count);

                for (var i = 0; i < routes.Count; i++)
                {
                    var routeBytes = Encoding.UTF8.GetBytes(routes[i].ToString());

                    writer.Write(routeBytes.Length);
                    writer.Write(routeBytes);
                }
                writer.Write(publish);
            }
        }

        private static void EncodeRouteRequest(IMessage message, Route route, bool publish, EndPointAddress endPoint)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route.ToString());

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.RouteToEndPoint);
                writer.Write((short)0);
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
                writer.Write(endPoint);

                writer.Write(publish);
            }
        }

        private static void EncodeRegisterRouteRequest(IMessage message, Route route, RouteRegistrationOptions options)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route.ToString());

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.RegisterRoute);
                writer.Write((short)0);
                writer.Write((int)options);
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
            }
        }

        private static void EncodeUnregisterRouteRequest(IMessage message, Route route)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route.ToString());

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.UnregisterRoute);
                writer.Write((short)0);
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
            }
        }

        private static void EncodeUnregisterRouteRequest(IMessage message, bool removePersistentRoutes)
        {
            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.UnregisterRoute);
                writer.Write((short)0);
                writer.Write(removePersistentRoutes);
            }
        }

        #endregion

        #region Send

        private Task<IMessage> SendInternalAsync(IMessage message, CancellationToken cancellation)
        {
            var now = _dateTimeProvider.GetCurrentTime();

            return SendInternalAsync(message, now, cancellation);
        }

        private async Task<IMessage> SendInternalAsync(IMessage message, DateTime now, CancellationToken cancellation)
        {
            return await _logicalEndPoint.SendAsync(message, cancellation);
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            // We cache the delegate for perf reasons.
            var handler = new Func<IMessage, CancellationToken, Task<(IMessage message, bool handled)>>(HandleAsync);

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
                    // TODO: Log
                }
            }
        }

        private async Task<(IMessage message, bool handled)> HandleAsync(IMessage message, CancellationToken cancellation)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var messageType = (MessageType)reader.ReadInt16();
                reader.ReadInt16();

                switch (messageType)
                {
                    case MessageType.Handle:
                        {
                            var routeBytesLength = reader.ReadInt32();
                            var routeBytes = reader.ReadBytes(routeBytesLength);
                            var route = Encoding.UTF8.GetString(routeBytes);
                            var publish = reader.ReadBoolean();
                            var isLocalDispatch = reader.ReadBoolean();
                            var (response, handled) = await ReceiveHandleRequestAsync(message, new Route(route), publish, isLocalDispatch, cancellation);
                            Assert(response.FrameIndex == response.FrameCount - 1);
                            return (response, handled);
                        }

                    default:
                        {
                            // TODO: Send bad request message
                            // TODO: Log

                            return default;
                        }
                }
            }
        }

        private ValueTask<(IMessage message, bool handled)> ReceiveHandleRequestAsync(IMessage message, Route route, bool publish, bool isLocalDispatch, CancellationToken cancellation)
        {
            return _serializedMessageHandler.HandleAsync(route, message, publish, isLocalDispatch, cancellation);
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource != null)
            {
                disposalSource.Cancel();
                _receiveProcess.Terminate();
                disposalSource.Dispose();
            }
        }

        private IDisposable CheckDisposal(ref CancellationToken cancellation,
                                          out CancellationToken disposal)
        {
            var disposalSource = _disposalSource; // Volatile read op

            if (disposalSource == null)
                throw new ObjectDisposedException(GetType().FullName);

            disposal = disposalSource.Token;

            if (cancellation.CanBeCanceled)
            {
                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, disposal);
                cancellation = combinedCancellationSource.Token;

                return combinedCancellationSource;
            }
            else
            {
                cancellation = disposal;

                return NoOpDisposable.Instance;
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

    public sealed class RemoteMessageRouterFactory : IMessageRouterFactory
    {
        private readonly IRequestReplyClientEndPoint _logicalEndPoint;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILoggerFactory _loggerFactory;

        public RemoteMessageRouterFactory(IRequestReplyClientEndPoint logicalEndPoint,
                                          IDateTimeProvider dateTimeProvider,
                                          ILoggerFactory loggerFactory = null)
        {
            if (logicalEndPoint == null)
                throw new ArgumentNullException(nameof(logicalEndPoint));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _logicalEndPoint = logicalEndPoint;
            _dateTimeProvider = dateTimeProvider;
            _loggerFactory = loggerFactory;
        }

        public ValueTask<EndPointAddress> GetDefaultEndPointAsync(CancellationToken cancellation)
        {
            return _logicalEndPoint.GetLocalEndPointAsync();
        }

        public IMessageRouter CreateMessageRouter(ISerializedMessageHandler serializedMessageHandler)
        {
            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            var logger = _loggerFactory?.CreateLogger<RemoteMessageRouter>();
            return new RemoteMessageRouter(serializedMessageHandler, _logicalEndPoint, _dateTimeProvider, logger);
        }

        public IMessageRouter CreateMessageRouter(EndPointAddress logicalEndPoint, ISerializedMessageHandler serializedMessageHandler)
        {
            throw new NotSupportedException();
        }
    }
}
