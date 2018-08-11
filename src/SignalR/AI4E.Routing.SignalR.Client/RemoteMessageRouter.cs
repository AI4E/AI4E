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

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    // TODO: Logging   
    public sealed class RemoteMessageRouter : IMessageRouter, IAsyncDisposable
    {
        private static readonly TimeSpan _leaseLength = TimeSpan.FromMinutes(5); // TODO: This should be synced with the server
        private static readonly TimeSpan _leaseLengthHalf = new TimeSpan(_leaseLength.Ticks / 2);

        private readonly ISerializedMessageHandler _serializedMessageHandler;
        private readonly ILogicalClientEndPoint _logicalEndPoint;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<RemoteMessageRouter> _logger;

        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncProcess _pingProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        private DateTime _lastSendTime = DateTime.MinValue;
        private readonly object _lastSendTimeLock = new object();

        public RemoteMessageRouter(ISerializedMessageHandler serializedMessageHandler,
                                   ILogicalClientEndPoint logicalEndPoint,
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

            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _pingProcess = new AsyncProcess(PingProcess);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region IMessageRouter

        public ValueTask<EndPointRoute> GetLocalEndPointAsync(CancellationToken cancellation = default)
        {
            return _logicalEndPoint.GetLocalEndPointAsync(cancellation);
        }

        public async ValueTask<IReadOnlyCollection<IMessage>> RouteAsync(IEnumerable<string> routes, IMessage serializedMessage, bool publish, CancellationToken cancellation = default)
        {
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));

            if (routes.Any(p => p == null))
                throw new ArgumentException("The collection must not contain null values.", nameof(routes));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            var idx = serializedMessage.FrameIndex;

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

        public async ValueTask<IMessage> RouteAsync(string route, IMessage serializedMessage, bool publish, EndPointRoute endPoint, CancellationToken cancellation = default)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (serializedMessage == null)
                throw new ArgumentNullException(nameof(serializedMessage));

            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            var idx = serializedMessage.FrameIndex;

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

        public Task RegisterRouteAsync(string route, CancellationToken cancellation = default)
        {
            var message = new Message();
            EncodeRegisterRouteRequest(message, route);
            return SendInternalAsync(message, cancellation);
        }

        public Task UnregisterRouteAsync(string route, CancellationToken cancellation = default)
        {
            var message = new Message();
            EncodeUnregisterRouteRequest(message, route);
            return SendInternalAsync(message, cancellation);
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

        private static void EncodeRouteRequest(IMessage message, IEnumerable<string> routes, bool publish)
        {
            var routesBytes = routes.Select(route => Encoding.UTF8.GetBytes(route)).ToList();
            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.Route);
                writer.Write((short)0); // Padding
                writer.Write(routesBytes.Count);

                for (var i = 0; i < routesBytes.Count; i++)
                {
                    writer.Write(routesBytes[i].Length);
                    writer.Write(routesBytes[i]);
                }
                writer.Write(publish);
            }
        }

        private static void EncodeRouteRequest(IMessage message, string route, bool publish, EndPointRoute endPoint)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route);
            var endPointBytes = Encoding.UTF8.GetBytes(endPoint.Route);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.RouteToEndPoint);
                writer.Write((short)0);
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
                writer.Write(endPointBytes.Length);
                writer.Write(endPointBytes);
                writer.Write(publish);
            }
        }

        private static void EncodeRegisterRouteRequest(IMessage message, string route)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.RegisterRoute);
                writer.Write((short)0);
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
            }
        }

        private static void EncodeUnregisterRouteRequest(IMessage message, string route)
        {
            var routeBytes = Encoding.UTF8.GetBytes(route);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.UnregisterRoute);
                writer.Write((short)0);
                writer.Write(routeBytes.Length);
                writer.Write(routeBytes);
            }
        }

        private static void EncodePing(IMessage message)
        {
            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)MessageType.Ping);
                writer.Write((short)0);
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
            lock (_lastSendTimeLock)
            {
                if (_lastSendTime < now)
                {
                    _lastSendTime = now;
                }
            }

            return await _logicalEndPoint.SendAsync(message, cancellation);
        }

        #endregion

        #region Ping

        private async Task PingProcess(CancellationToken cancellation)
        {
            await _initializationHelper.Initialization.WithCancellation(cancellation);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var now = _dateTimeProvider.GetCurrentTime();

                    DateTime lastSendTime;

                    lock (_lastSendTimeLock)
                    {
                        lastSendTime = _lastSendTime;
                    }

                    if (lastSendTime > now)
                    {
                        now = lastSendTime;
                    }

                    Assert(now >= lastSendTime);

                    var timeSinceLastSend = now - lastSendTime;

                    Assert(timeSinceLastSend >= TimeSpan.Zero);

                    if (timeSinceLastSend >= _leaseLengthHalf)
                    {
                        // This will set the last send time to now.
                        await SendPingAsync(now, cancellation);

                        continue;
                    }
                    else
                    {
                        var timeToWait = _leaseLengthHalf - timeSinceLastSend;

                        Assert(timeToWait > TimeSpan.Zero);

                        await Task.Delay(timeToWait, cancellation);
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log
                }
            }
        }

        private async Task SendPingAsync(DateTime now, CancellationToken cancellation)
        {
            var pingMessage = new Message();
            EncodePing(pingMessage);
            await SendInternalAsync(pingMessage, now, cancellation);
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            await _initializationHelper.Initialization.WithCancellation(cancellation);

            var semaphore = new SemaphoreSlim(10);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    // Throttle the receive process to 10 concurrent requests
                    await semaphore.WaitAsync(cancellation);

                    Task.Run(async () =>
                    {
                        try
                        {
                            await _logicalEndPoint.ReceiveAsync(ReceiveAsync, cancellation);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log
                }
            }
        }

        private async Task<IMessage> ReceiveAsync(IMessage message, CancellationToken cancellation)
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
                            var response = await ReceiveHandleRequestAsync(message, route, publish, cancellation);
                            Assert(response.FrameIndex == response.FrameCount - 1);
                            return response;
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

        private ValueTask<IMessage> ReceiveHandleRequestAsync(IMessage message, string route, bool publish, CancellationToken cancellation)
        {
            return _serializedMessageHandler.HandleAsync(route, message, publish, cancellation);
        }

        #endregion

        #region Init

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _receiveProcess.StartAsync(cancellation);
            await _pingProcess.StartAsync(cancellation);
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
            await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);
            await _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger);
            await _pingProcess.TerminateAsync().HandleExceptionsAsync(_logger);
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

    public sealed class RemoteMessageRouterFactory : IMessageRouterFactory
    {
        private readonly ILogicalClientEndPoint _logicalEndPoint;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILoggerFactory _loggerFactory;

        public RemoteMessageRouterFactory(ILogicalClientEndPoint logicalEndPoint,
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

        public IMessageRouter CreateMessageRouter(ISerializedMessageHandler serializedMessageHandler)
        {
            if (serializedMessageHandler == null)
                throw new ArgumentNullException(nameof(serializedMessageHandler));

            var logger = _loggerFactory?.CreateLogger<RemoteMessageRouter>();
            return new RemoteMessageRouter(serializedMessageHandler, _logicalEndPoint, _dateTimeProvider, logger);
        }

        public IMessageRouter CreateMessageRouter(EndPointRoute logicalEndPoint, ISerializedMessageHandler serializedMessageHandler)
        {
            throw new NotSupportedException();
        }
    }
}
