using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.EndPointManagement
{
    public sealed class EndPointManager<TAddress> : IEndPointManager<TAddress>, IRemoteEndPointManager<TAddress>, IAsyncDisposable
    {
        #region Fields

        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncProcess _receiveProcess;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly IMessageCoder<TAddress> _messageCoder;
        private readonly ILocalEndPointFactory<TAddress> _endPointFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<EndPointRoute, ILocalEndPoint<TAddress>> _endPoints;
        private readonly ConcurrentDictionary<EndPointRoute, RemoteEndPoint<TAddress>> _remoteEndPoints;

        #endregion

        #region C'tor

        public EndPointManager(IPhysicalEndPoint<TAddress> physicalEndPoint,
                               IRouteMap<TAddress> routeManager,
                               IMessageCoder<TAddress> messageCoder,
                               ILocalEndPointFactory<TAddress> endPointFactory,
                               IServiceProvider serviceProvider,
                               ILogger<EndPointManager<TAddress>> logger)
        {
            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (endPointFactory == null)
                throw new ArgumentNullException(nameof(endPointFactory));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            PhysicalEndPoint = physicalEndPoint;
            _routeManager = routeManager;
            _messageCoder = messageCoder;
            _endPointFactory = endPointFactory;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _endPoints = new ConcurrentDictionary<EndPointRoute, ILocalEndPoint<TAddress>>();
            _remoteEndPoints = new ConcurrentDictionary<EndPointRoute, RemoteEndPoint<TAddress>>();

            _receiveProcess = new AsyncProcess(ReceiveProcedure);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);

        }

        #endregion

        public TAddress LocalAddress => PhysicalEndPoint.LocalAddress;
        public IPhysicalEndPoint<TAddress> PhysicalEndPoint { get; }

        #region EndPoints

        public async Task AddEndPointAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

                var endPoint = _endPointFactory.CreateLocalEndPoint(this, this, localEndPoint);

                if (_endPoints.TryAdd(localEndPoint, endPoint))
                {
                    _logger?.LogInformation($"Registered end-point {localEndPoint.Route}");
                    await endPoint.Initialization.WithCancellation(cancellationSource.Token);
                }
            }
        }

        public async Task RemoveEndPointAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

                if (_endPoints.TryRemove(localEndPoint, out var endPoint))
                {
                    await endPoint.DisposeAsync().WithCancellation(cancellationSource.Token);
                    _logger?.LogInformation($"Unregistered end-point {localEndPoint.Route}");
                }
            }
        }

        internal bool TryGetEndPoint(EndPointRoute localEndPoint, out ILocalEndPoint<TAddress> endPoint)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            Assert(localEndPoint != null);

            return _endPoints.TryGetValue(localEndPoint, out endPoint);
        }

        bool IEndPointManager<TAddress>.TryGetEndPoint(EndPointRoute localEndPoint, out ILocalEndPoint<TAddress> endPoint)
        {
            return TryGetEndPoint(localEndPoint, out endPoint);
        }

        #endregion

        #region RemoteEndPoints

        public IRemoteEndPoint<TAddress> GetRemoteEndPoint(EndPointRoute remoteEndPoint)
        {
            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            var logger = _serviceProvider.GetService<ILogger<RemoteEndPoint<TAddress>>>();

            return _remoteEndPoints.GetOrAdd(remoteEndPoint, _ => new RemoteEndPoint<TAddress>(this, remoteEndPoint, _messageCoder, _routeManager, logger));
        }

        #endregion

        #region Phyical end point

        private async Task ReceiveProcedure(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, localAddress, remoteAddress, remoteEndPoint, localEndPoint, messageType) = await ReceiveAsync(cancellation);

                    Task.Run(() => HandleMessageAsync(message, localAddress, remoteAddress, remoteEndPoint, localEndPoint, messageType, cancellation)).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger.LogWarning(exc, "An error occured while receiving a message.");
                }
            }
        }

        private async Task<(IMessage message, TAddress localAddress, TAddress remoteAddress, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, MessageType messageType)> ReceiveAsync(CancellationToken cancellation)
        {
            // Receive a single message
            var message = await PhysicalEndPoint.ReceiveAsync(cancellation);

            // Decode first message frame
            return _messageCoder.DecodeMessage(message);
        }

        private Task SendMisroutedAsync(TAddress remoteAddress, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            var message = _messageCoder.EncodeMessage(LocalAddress, remoteAddress, remoteEndPoint, localEndPoint, MessageType.Misrouted);
            return PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
        }

        private Task SendEndPointNotPresentAsync(TAddress remoteAddress, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            var message = _messageCoder.EncodeMessage(LocalAddress, remoteAddress, remoteEndPoint, localEndPoint, MessageType.EndPointNotPresent);
            return PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
        }

        private async Task HandleMessageAsync(IMessage message, TAddress localAddress, TAddress remoteAddress, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, MessageType messageType, CancellationToken cancellation)
        {
            if (!localAddress.Equals(LocalAddress))
            {
                await SendMisroutedAsync(remoteAddress, remoteEndPoint, localEndPoint, cancellation);
                return;
            }

            switch (messageType)
            {
                case MessageType.Message:
                    {
                        _logger?.LogTrace($"Received message from address {remoteAddress}, end-point {remoteEndPoint} for end-point {localEndPoint}.");

                        var endPoint = await GetLocalEndPointAsync(remoteAddress, remoteEndPoint, localEndPoint, cancellation);

                        if (endPoint != null)
                        {
                            await endPoint.OnReceivedAsync(message, remoteAddress, remoteEndPoint, cancellation);
                        }

                        break;
                    }
                case MessageType.Signal:
                    {
                        _logger?.LogTrace($"Received signal from address {remoteAddress}, end-point {remoteEndPoint} for end-point {localEndPoint}.");

                        var endPoint = await GetLocalEndPointAsync(remoteAddress, remoteEndPoint, localEndPoint, cancellation);

                        if (endPoint != null)
                        {
                            await endPoint.OnSignalledAsync(remoteAddress, cancellation);
                        }

                        await endPoint.OnSignalledAsync(remoteAddress, cancellation);
                        break;
                    }
                case MessageType.Request:
                    _logger?.LogTrace($"Received request from address {remoteAddress}, end-point {remoteEndPoint} for end-point {localEndPoint}.");
                    await GetRemoteEndPoint(remoteEndPoint).OnRequestAsync(remoteAddress, cancellation);
                    break;

                case MessageType.EndPointNotPresent:
                    /* TODO */
                    break;

                case MessageType.ProtocolNotSupported:
                    /* TODO */
                    break;

                case MessageType.Unknown:
                default:
                    /* TODO */
                    break;
            }
        }

        private async Task<ILocalEndPoint<TAddress>> GetLocalEndPointAsync(TAddress remoteAddress, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (!TryGetEndPoint(localEndPoint, out var endPoint))
            {
                await SendEndPointNotPresentAsync(remoteAddress, remoteEndPoint, localEndPoint, cancellation);
                return null;
            }

            return endPoint;
        }

        #endregion

        public async Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            try
            {
                using (await _disposeHelper.ProhibitDisposalAsync())
                {
                    if (_disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(GetType().FullName);

                    var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

                    if (!TryGetEndPoint(localEndPoint, out var endPoint))
                    {
                        throw new EndPointNotFoundException("The specified local endpoint was not found.");
                    }

                    return await endPoint.ReceiveAsync(cancellationSource.Token);
                }
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            try
            {
                using (await _disposeHelper.ProhibitDisposalAsync())
                {
                    if (_disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(GetType().FullName);

                    var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

                    if (!TryGetEndPoint(localEndPoint, out var endPoint))
                    {
                        throw new EndPointNotFoundException("The specified local endpoint was not found.");
                    }

                    await endPoint.SendAsync(message, remoteEndPoint, cancellationSource.Token);
                }
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // We need to push the frame in order that the decoder can pop it
            request.PushFrame();

            TAddress remoteAddress;
            EndPointRoute localEndPoint, remoteEndPoint;

            try
            {
                (_, _, remoteAddress, remoteEndPoint, localEndPoint, _) = _messageCoder.DecodeMessage(request);
            }
            catch (Exception exc)
            {
                throw new ArgumentException("The message is not formatted as expected.", exc);
            }

            Assert(remoteAddress != null);
            Assert(remoteEndPoint != null);
            Assert(localEndPoint != null);

            try
            {
                using (await _disposeHelper.ProhibitDisposalAsync())
                {
                    if (_disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(GetType().FullName);

                    var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

                    if (!TryGetEndPoint(localEndPoint, out var endPoint))
                    {
                        throw new EndPointNotFoundException("The specified local endpoint was not found.");
                    }

                    await endPoint.SendAsync(response, remoteEndPoint, remoteAddress, cancellationSource.Token);
                }
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _receiveProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

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
            await _initializationHelper.CancelAsync();
            await _receiveProcess.TerminateAsync();
        }

        public Task Disposal => _disposeHelper.Disposal;

        #endregion
    }

    public enum MessageType : int
    {
        /// <summary>
        /// An unknown message type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A normal (user) message.
        /// </summary>
        Message = 1,

        /// <summary>
        /// A request for a (user) message.
        /// </summary>
        Request = 2,

        /// <summary>
        /// A signal that one or multiple (user) messages are available for request.
        /// </summary>
        Signal = 3,

        /// <summary>
        /// The protocol of a received message is not supported. The payload is the seq-num of the message in raw format.
        /// </summary>
        ProtocolNotSupported = -1,

        EndPointNotPresent = -2,

        Misrouted = -3
    }
}
