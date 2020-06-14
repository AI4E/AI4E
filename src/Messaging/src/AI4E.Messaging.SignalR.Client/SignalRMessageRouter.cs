using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils;
using AI4E.Utils.Messaging.Primitives;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;

namespace AI4E.Messaging.SignalR.Client
{
    // TODO: Logging   
    public sealed class SignalRMessageRouter : IMessageRouter
    {
        private readonly IRouteMessageHandler _routeMessageHandler;
        private readonly ISignalRClientEndPoint _clientEndPoint;
        private readonly ILogger<SignalRMessageRouter> _logger;

        private readonly IAsyncProcess _receiveProcess;
        private volatile CancellationTokenSource _disposalSource = new CancellationTokenSource();

        public SignalRMessageRouter(IRouteMessageHandler routeMessageHandler,
                                    ISignalRClientEndPoint clientEndPoint,
                                    ILogger<SignalRMessageRouter> logger = null)
        {
            if (routeMessageHandler == null)
                throw new ArgumentNullException(nameof(routeMessageHandler));

            if (clientEndPoint == null)
                throw new ArgumentNullException(nameof(clientEndPoint));

            _routeMessageHandler = routeMessageHandler;
            _clientEndPoint = clientEndPoint;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
        }

        #region IMessageRouter

        public ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return _clientEndPoint.GetLocalEndPointAsync(cancellation);
        }

        public async ValueTask<IReadOnlyCollection<RouteMessage<IDispatchResult>>> RouteAsync(
            RouteHierarchy routes,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            CancellationToken cancellation)
        {
            if (routes.Any(p => p == null))
                throw new ArgumentException("The collection must not contain null values.", nameof(routes));

            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = routeMessage.Message;

                    EncodeRouteRequest(ref message, routes, publish);

                    var response = await _clientEndPoint.SendAsync(message, cancellation);
                    var result = DecodeRouteResponse(response);

                    return result.Select(p => new RouteMessage<IDispatchResult>(p)).ToImmutableList();
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async ValueTask<RouteMessage<IDispatchResult>> RouteAsync(
            Route route,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = routeMessage.Message;
                    EncodeRouteRequest(ref message, route, publish, endPoint);
                    var response = await _clientEndPoint.SendAsync(message, cancellation);
                    return new RouteMessage<IDispatchResult>(response.Message);

                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task RegisterRouteAsync(
            RouteRegistration routeRegistration,
            CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = new Message();
                    EncodeRegisterRouteRequest(ref message, routeRegistration.Route, routeRegistration.RegistrationOptions);
                    await _clientEndPoint.SendAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task UnregisterRouteAsync(
            Route route,
            CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = new Message();
                    EncodeUnregisterRouteRequest(ref message, route);
                    await _clientEndPoint.SendAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task UnregisterRoutesAsync(bool removePersistentRoutes, CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    var message = new Message();
                    EncodeUnregisterRouteRequest(ref message, removePersistentRoutes);
                    await _clientEndPoint.SendAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        #endregion

        #region Encoding/Decoding

        private static IReadOnlyCollection<Message> DecodeRouteResponse(in MessageSendResult sendResult)
        {
            sendResult.Message.PopFrame(out var frame);
            Message[] result;

            using (var frameStream = frame.OpenStream())
            using (var reader = new BinaryReader(frameStream))
            {
                var resultCount = PrefixCodingHelper.Read7BitEncodedInt(reader);
                result = new Message[resultCount];

                for (var i = 0; i < resultCount; i++)
                {
                    result[i] = Message.ReadFromStream(frameStream);
                }
            }

            return result;
        }

        private static void EncodeRouteRequest(ref Message message, in RouteHierarchy routes, bool publish)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write((short)MessageType.Route);
                writer.Write(publish);
                RouteHierarchy.Write(writer, routes);
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        private static void EncodeRouteRequest(ref Message message, in Route route, bool publish, in RouteEndPointAddress endPoint)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write((short)MessageType.RouteToEndPoint);
                writer.Write(publish);
                Route.Write(writer, route);
                writer.Write(endPoint);
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        private static void EncodeRegisterRouteRequest(ref Message message, Route route, RouteRegistrationOptions options)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write((short)MessageType.RegisterRoute);
                writer.Write((int)options);
                Route.Write(writer, route);
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        private static void EncodeUnregisterRouteRequest(ref Message message, Route route)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write((short)MessageType.UnregisterRoute);
                Route.Write(writer, route);
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        private static void EncodeUnregisterRouteRequest(ref Message message, bool removePersistentRoutes)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write((short)MessageType.UnregisterRoute);
                writer.Write(removePersistentRoutes);
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var receiveResult = await _clientEndPoint.ReceiveAsync(cancellation);
                    HandleAsync(receiveResult, cancellation)
                        .HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch
                {
                    // TODO: Log
                }
            }
        }

        private async Task HandleAsync(MessageReceiveResult<Packet> receiveResult, CancellationToken cancellation)
        {
            using var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, receiveResult.Cancellation);

            cancellation = combinedCancellationSource.Token;

            try
            {
                var (response, handled) = await HandleAsync(receiveResult.Message, cancellation);

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

        private async Task<(Message message, bool handled)> HandleAsync(
            Message message, CancellationToken cancellation)
        {
            message = message.PopFrame(out var frame);
            using var stream = frame.OpenStream();
            using var reader = new BinaryReader(stream);
            var messageType = (MessageType)reader.ReadInt16();

            switch (messageType)
            {
                case MessageType.Handle:
                    {
                        var publish = reader.ReadBoolean();
                        var isLocalDispatch = reader.ReadBoolean();
                        Route.Read(reader, out var route);
                        var (response, handled) = await ReceiveHandleRequestAsync(
                            message, route, publish, isLocalDispatch, cancellation);
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

        private async ValueTask<(Message message, bool handled)> ReceiveHandleRequestAsync(
            Message message, Route route, bool publish, bool isLocalDispatch, CancellationToken cancellation)
        {
            var routeMessage = new RouteMessage<DispatchDataDictionary>(message);
            var routeMessageResult = await _routeMessageHandler.HandleAsync(
                routeMessage, route, publish, isLocalDispatch, cancellation);

            return (routeMessageResult.RouteMessage.Message, routeMessageResult.Handled);
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

    public sealed class SignalRMessageRouterFactory : IMessageRouterFactory
    {
        private readonly ISignalRClientEndPoint _clientEndPoint;
        private readonly ILoggerFactory _loggerFactory;

        public SignalRMessageRouterFactory(
            ISignalRClientEndPoint clientEndPoint,
            ILoggerFactory loggerFactory = null)
        {
            if (clientEndPoint == null)
                throw new ArgumentNullException(nameof(clientEndPoint));

            _clientEndPoint = clientEndPoint;
            _loggerFactory = loggerFactory;
        }

        public ValueTask<RouteEndPointAddress> GetDefaultEndPointAsync(CancellationToken cancellation)
        {
            return _clientEndPoint.GetLocalEndPointAsync();
        }

        public ValueTask<IMessageRouter> CreateMessageRouterAsync(
            IRouteMessageHandler routeMessageHandler,
            CancellationToken cancellation)
        {
            if (routeMessageHandler == null)
                throw new ArgumentNullException(nameof(routeMessageHandler));

            var logger = _loggerFactory?.CreateLogger<SignalRMessageRouter>();

            return new ValueTask<IMessageRouter>(
                new SignalRMessageRouter(routeMessageHandler, _clientEndPoint, logger));
        }

        public async ValueTask<IMessageRouter> CreateMessageRouterAsync(
            RouteEndPointAddress endPoint,
            IRouteMessageHandler routeMessageHandler,
            CancellationToken cancellation)
        {
            var defaultEndPoint = await GetDefaultEndPointAsync(cancellation);

            if (defaultEndPoint == endPoint)
            {
                return await CreateMessageRouterAsync(routeMessageHandler, cancellation);
            }

            // TODO: Shall we allow this?
            throw new NotSupportedException();
        }
    }
}
