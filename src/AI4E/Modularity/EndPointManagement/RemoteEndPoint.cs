using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.EndPointManagement
{
    public class RemoteEndPoint<TAddress> : IRemoteEndPoint<TAddress>, IAsyncDisposable
    {
        private readonly IEndPointManager<TAddress> _endPointManager;
        private readonly IMessageCoder<TAddress> _messageCoder;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly ILogger<RemoteEndPoint<TAddress>> _logger;
        private readonly ConcurrentQueue<(IMessage message, EndPointRoute localEndPoint)> _txQueue;

        private readonly AsyncProcess _mapUpdateProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;


        private readonly Dictionary<TAddress, bool> _replica = new Dictionary<TAddress, bool>();

        public RemoteEndPoint(IEndPointManager<TAddress> endPointManager,
                              EndPointRoute route,
                              IMessageCoder<TAddress> messageCoder,
                              IRouteMap<TAddress> routeManager,
                              ILogger<RemoteEndPoint<TAddress>> logger)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            _endPointManager = endPointManager;
            Route = route;
            _messageCoder = messageCoder;
            _routeManager = routeManager;
            _logger = logger;

            _txQueue = new ConcurrentQueue<(IMessage message, EndPointRoute localEndPoint)>();

            _mapUpdateProcess = new AsyncProcess(MapUpdateProcess);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
        }

        public EndPointRoute Route { get; }
        public TAddress LocalAddress => _endPointManager.LocalAddress;
        public IPhysicalEndPoint<TAddress> PhysicalEndPoint => _endPointManager.PhysicalEndPoint;

        public async Task SendAsync(IMessage message, EndPointRoute localEndPoint, TAddress remoteAddress, CancellationToken cancellation)
        {
            var frameIdx = message.FrameIndex;
            _messageCoder.EncodeMessage(message, LocalAddress, remoteAddress, Route, localEndPoint, MessageType.Message);

            try
            {
                await PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PopFrame();
                Assert(frameIdx == message.FrameIndex);
                throw;
            }
        }

        private Task SignalAsync(TAddress remoteAddress, CancellationToken cancellation)
        {
            var message = _messageCoder.EncodeMessage(LocalAddress, remoteAddress, Route, default, MessageType.Signal);
            return PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
        }

        public async Task SendAsync(IMessage message, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            _txQueue.Enqueue((message, localEndPoint));

            ImmutableArray<TAddress> replica;

            lock (_replica)
            {
                replica = _replica.Where(p => !p.Value).Select(p => p.Key).ToImmutableArray();

                foreach (var entry in replica)
                {
                    _replica[entry] = true;
                }
            }

            await Task.WhenAll(replica.Select(p => SignalAsync(p, cancellation)));
        }

        public async Task OnRequestAsync(TAddress remoteAddress, CancellationToken cancellation)
        {
            if (!_txQueue.TryDequeue(out var entry))
            {
                lock (_replica)
                {
                    _replica[remoteAddress] = false;
                }
            }
            else
            {
                try
                {
                    await SendAsync(entry.message, entry.localEndPoint, remoteAddress, cancellation);
                }
                catch
                {
                    _txQueue.Enqueue(entry);

                    throw;
                }

                lock (_replica)
                {
                    _replica[remoteAddress] = true;
                }
            }
        }

        private async Task MapUpdateProcess(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Started map update process for remote end-point '{Route}'.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    await UpdateAddressList(cancellation);
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Failure while updating maps for remote end-point '{Route}'.");
                }
            }
        }

        private async Task UpdateAddressList(CancellationToken cancellation)
        {
            var addresses = new List<TAddress>(await _routeManager.GetMapsAsync(Route, cancellation));

            IEnumerable<TAddress> newAddresses;

            lock (_replica)
            {
                var currAddresses = _replica.Keys;
                newAddresses = addresses.Except(currAddresses).ToArray();

                foreach (var address in newAddresses)
                {
                    _replica[address] = true;
                }

                foreach (var address in currAddresses.Except(addresses))
                {
                    _replica.Remove(address);
                }
            }

            await Task.WhenAll(newAddresses.Select(p => SignalAsync(p, cancellation: default)));
        }

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _mapUpdateProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        private async Task DisposeInternalAsync()
        {
            try
            {
                await _initializationHelper.CancelAsync();
            }
            finally
            {
                await _mapUpdateProcess.TerminateAsync();
            }
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
    }
}
