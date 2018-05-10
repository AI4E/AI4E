/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EndPointManager.cs 
 * Types:           AI4E.Routing.EndPointManager'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class EndPointManager<TAddress> : IEndPointManager<TAddress>, IRemoteEndPointManager<TAddress>, IAsyncDisposable
    {
        #region Fields

        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly IMessageCoder<TAddress> _messageCoder;
        private readonly ILocalEndPointFactory<TAddress> _endPointFactory;
        private readonly IEndPointScheduler<TAddress> _endPointScheduler;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        private readonly Dictionary<EndPointRoute, ILocalEndPoint<TAddress>> _endPoints;
        private readonly ConcurrentDictionary<EndPointRoute, RemoteEndPoint<TAddress>> _remoteEndPoints;

        #endregion

        #region C'tor

        public EndPointManager(IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                               IRouteMap<TAddress> routeManager,
                               IMessageCoder<TAddress> messageCoder,
                               ILocalEndPointFactory<TAddress> endPointFactory,
                               IEndPointScheduler<TAddress> endPointScheduler,
                               IServiceProvider serviceProvider,
                               ILogger<EndPointManager<TAddress>> logger)
        {
            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (endPointFactory == null)
                throw new ArgumentNullException(nameof(endPointFactory));

            if (endPointScheduler == null)
                throw new ArgumentNullException(nameof(endPointScheduler));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _endPointMultiplexer = endPointMultiplexer;
            _routeManager = routeManager;
            _messageCoder = messageCoder;
            _endPointFactory = endPointFactory;
            _endPointScheduler = endPointScheduler;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _endPoints = new Dictionary<EndPointRoute, ILocalEndPoint<TAddress>>();
            _remoteEndPoints = new ConcurrentDictionary<EndPointRoute, RemoteEndPoint<TAddress>>();
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        public TAddress LocalAddress => _endPointMultiplexer.LocalAddress;

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

                ILocalEndPoint<TAddress> endPoint;

                Assert(_endPoints != null);

                lock (_endPoints)
                {
                    if (!_endPoints.TryGetValue(localEndPoint, out endPoint) || endPoint.IsDisposed)
                    {
                        var physicalEndPoint = GetMultiplexEndPoint(localEndPoint); // TODO: Use async API

                        endPoint = _endPointFactory.CreateLocalEndPoint(this, this, physicalEndPoint, localEndPoint);
                        _endPoints[localEndPoint] = endPoint;

                        _logger?.LogInformation($"Registered end-point {localEndPoint.Route}");
                    }
                }

                await endPoint.Initialization.WithCancellation(cancellationSource.Token);
            }
        }

        private IPhysicalEndPoint<TAddress> GetMultiplexEndPoint(EndPointRoute route)
        {
            return _endPointMultiplexer.GetMultiplexEndPoint("end-points/" + route.Route);
        }

        private Task<IPhysicalEndPoint<TAddress>> GetMultiplexEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            return _endPointMultiplexer.GetMultiplexEndPointAsync("end-points/" + route.Route, cancellation);
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

                Assert(_endPoints != null);

                if (TryGetEndPoint(localEndPoint, out var endPoint))
                {
                    await endPoint.DisposeAsync().WithCancellation(cancellationSource.Token);
                }

                lock (_endPoints)
                {
                    if (_endPoints.TryGetValue(localEndPoint, out var comparand) && comparand == endPoint)
                    {
                        _endPoints.Remove(localEndPoint);
                        _logger?.LogInformation($"Unregistered end-point {localEndPoint.Route}");
                    }
                }
            }
        }

        internal bool TryGetEndPoint(EndPointRoute localEndPoint, out ILocalEndPoint<TAddress> endPoint)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            Assert(localEndPoint != null);
            Assert(_endPoints != null);

            lock (_endPoints)
            {
                if (_endPoints.TryGetValue(localEndPoint, out endPoint))
                {
                    if (endPoint.IsDisposed)
                    {
                        _endPoints.Remove(localEndPoint);

                        endPoint = default;
                        return false;
                    }

                    return true;
                }
            }

            return false;
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

            return _remoteEndPoints.GetOrAdd(remoteEndPoint, CreateRemoteEndPoint);
        }

        private RemoteEndPoint<TAddress> CreateRemoteEndPoint(EndPointRoute remoteEndPoint)
        {
            var logger = _serviceProvider.GetService<ILogger<RemoteEndPoint<TAddress>>>();
            var physicalEndPointProvider = AsyncProvider.Create(cancellation => GetMultiplexEndPointAsync(remoteEndPoint, cancellation));

            return new RemoteEndPoint<TAddress>(endPointManager: this,
                                                physicalEndPointProvider, 
                                                remoteEndPoint, 
                                                _messageCoder, 
                                                _routeManager, 
                                                _endPointScheduler, 
                                                logger);
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
                throw new ArgumentException("The message is not formatted as expected.", nameof(request), exc);
            }

            Assert(remoteAddress != null);
            Assert(remoteEndPoint != null);
            Assert(localEndPoint != null);

            try
            {
                using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
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
            IEnumerable<ILocalEndPoint<TAddress>> localEndPoints;

            lock (_endPoints)
            {
                localEndPoints = _endPoints.Values;
            }

            await Task.WhenAll(localEndPoints.Select(p => p.DisposeAsync()));
        }

        public Task Disposal => _disposeHelper.Disposal;

        #endregion
    }
}
