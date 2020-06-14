using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Utils.Messaging.Primitives;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Messaging.Routing
{
    public sealed class RoutingSystem : IRoutingSystem
    {
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger<RoutingSystem>? _logger;

        private readonly Dictionary<RouteEndPointAddress, RouteEndPoint> _endPoints;
        private readonly object _endPointsLock = new object();

        private readonly AsyncDisposeHelper _disposeHelper;

        public RoutingSystem(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<RoutingSystem>();

            _endPoints = new Dictionary<RouteEndPointAddress, RouteEndPoint>();
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync, AsyncDisposeHelperOptions.Synchronize);
        }

        public async ValueTask<IRouteEndPoint?> GetEndPointAsync(RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            return await GetEndPointInternalAsync(endPoint, cancellation);
        }

        public async ValueTask<IRouteEndPoint> CreateEndPointAsync(RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation);

                // We have to ensure that only a single logical end-point exists for each address at any given time.
                lock (_endPointsLock)
                {
                    if (_endPoints.ContainsKey(endPoint))
                    {
                        throw new Exception("End point already present!"); // TODO
                    }

                    var logger = _loggerFactory?.CreateLogger<RouteEndPoint>();
                    var result = new RouteEndPoint(this, endPoint, logger);
                    _endPoints.Add(endPoint, result);
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #region Disposal

        /// <inheritdoc/>
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
           return _disposeHelper.DisposeAsync();
        }

        private async ValueTask DisposeInternalAsync()
        {
            await _endPoints.Values
                .Select(logicalEndPoint => logicalEndPoint.DisposeAsync())
                .WhenAll();
        }

        #endregion

        private async ValueTask<RouteEndPoint?> GetEndPointInternalAsync(
            RouteEndPointAddress endPoint, CancellationToken cancellation)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation);

                lock (_endPointsLock)
                {
                    if (!_endPoints.TryGetValue(endPoint, out var result))
                    {
                        result = null!;
                    }

                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private sealed class RouteEndPoint : IRouteEndPoint
        {
            private readonly RoutingSystem _routingSystem;
            private readonly ILogger<RouteEndPoint>? _logger;

            private readonly AsyncProducerConsumerQueue<RouteEndPointReceiveResult> _messages
                = new AsyncProducerConsumerQueue<RouteEndPointReceiveResult>();

            private readonly AsyncDisposeHelper _disposeHelper;

            public RouteEndPoint(
                RoutingSystem routingSystem,
                RouteEndPointAddress endPoint,
                ILogger<RouteEndPoint>? logger)
            {
                _routingSystem = routingSystem;
                EndPoint = endPoint;
                _logger = logger;

                _disposeHelper = new AsyncDisposeHelper(DisposeInternal, AsyncDisposeHelperOptions.Synchronize);
            }

            public RouteEndPointAddress EndPoint { get; }

            public async ValueTask<IRouteEndPointReceiveResult> ReceiveAsync(
                CancellationToken cancellation = default)
            {
                try
                {
                    using var guard = await _disposeHelper.GuardDisposalAsync(cancellation);
                    cancellation = guard.Cancellation;
                    return await _messages.DequeueAsync(cancellation);
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            public async ValueTask<RouteMessageHandleResult> SendAsync(
                Message message,
                RouteEndPointAddress remoteEndPoint,
                CancellationToken cancellation = default)
            {
                try
                {
                    using var guard = await _disposeHelper.GuardDisposalAsync(cancellation);
                    cancellation = guard.Cancellation;

                    if (remoteEndPoint == EndPoint)
                    {
                        return await SendCoreAsync(message, this, cancellation);
                    }

                    var routeEndPoint = await _routingSystem.GetEndPointInternalAsync(remoteEndPoint, cancellation);

                    if (routeEndPoint is null)
                        return default;

                    return await SendInternalAsync(message, routeEndPoint, cancellation);
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            private static async ValueTask<RouteMessageHandleResult> SendInternalAsync(
                Message message,
                RouteEndPoint routeEndPoint,
                CancellationToken cancellation)
            {
                try
                {
                    using var guard = await routeEndPoint._disposeHelper.GuardDisposalAsync(cancellation);
                    cancellation = guard.Cancellation;
                    return await SendCoreAsync(message, routeEndPoint, cancellation);
                }
                catch (OperationCanceledException) when (routeEndPoint._disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(routeEndPoint.GetType().FullName);
                }
            }

            private static async ValueTask<RouteMessageHandleResult> SendCoreAsync(
                Message message,
                RouteEndPoint routeEndPoint,
                CancellationToken cancellation)
            {
                var receiveResult = new RouteEndPointReceiveResult(message, routeEndPoint.EndPoint, cancellation);
                await routeEndPoint._messages.EnqueueAsync(receiveResult, cancellation);
                var routeResult = await receiveResult.Result;
                return routeResult;
            }

            /// <inheritdoc />
            public ValueTask DisposeAsync()
            {
                return _disposeHelper.DisposeAsync();
            }

            /// <inheritdoc />
            public void Dispose()
            {
                _disposeHelper.Dispose();
            }

            private void DisposeInternal()
            {
                // Remove us from the manager.
                lock (_routingSystem._endPointsLock)
                {
                    _routingSystem._endPoints.Remove(EndPoint, this);
                }
            }

            private sealed class RouteEndPointReceiveResult : IRouteEndPointReceiveResult
            {
                private readonly ValueTaskCompletionSource<RouteMessageHandleResult> _resultSource;

                public RouteEndPointReceiveResult(
                    Message message,
                    RouteEndPointAddress remoteEndPoint,
                    CancellationToken cancellation)
                {
                    Message = message;
                    RemoteEndPoint = remoteEndPoint;
                    Cancellation = cancellation;
                    _resultSource = ValueTaskCompletionSource.Create<RouteMessageHandleResult>();
                }

                public CancellationToken Cancellation { get; }

                public Message Message { get; }

                public RouteEndPointAddress RemoteEndPoint { get; }

                public ValueTask<RouteMessageHandleResult> Result => _resultSource.Task;

                public ValueTask SendResultAsync(RouteMessageHandleResult result)
                {
                    _resultSource.TrySetResult(result);
                    return default;
                }

                public ValueTask SendCancellationAsync()
                {
                    _resultSource.TrySetCanceled();
                    return default;
                }

                public ValueTask SendAckAsync()
                {
                    return SendResultAsync(
                        new RouteMessageHandleResult(routeMessage: default, handled: true));
                }
            }
        }
    }
}
