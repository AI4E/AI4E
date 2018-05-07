using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Remoting
{
    public sealed class EndPointMultiplexer<TAddress> : IEndPointMultiplexer<TAddress>, IAsyncDisposable
    {
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly Dictionary<string, MultiplexEndPoint> _endPoints = new Dictionary<string, MultiplexEndPoint>();
        private readonly IPhysicalEndPoint<TAddress> _physicalEndPoint;
        private readonly ILogger<EndPointMultiplexer<TAddress>> _logger;
        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public EndPointMultiplexer(IPhysicalEndPoint<TAddress> physicalEndPoint, ILogger<EndPointMultiplexer<TAddress>> logger)
        {
            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            _physicalEndPoint = physicalEndPoint;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public TAddress LocalAddress => _physicalEndPoint.LocalAddress;

        #region ReceiveProcess

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = await _physicalEndPoint.ReceiveAsync(cancellation);

                    Assert(message != null);

                    HandleMessageAsync(message, cancellation).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch
                {
                    // TODO: Logging
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message, CancellationToken cancellation)
        {
            Assert(message != null);

            var address = DecodeAddress(message);

            MultiplexEndPoint endPoint;
            bool endPointFound;

            using (await _lock.LockAsync(cancellation))
            {
                endPointFound = _endPoints.TryGetValue(address, out endPoint);
            }

            if (endPointFound)
            {
                await endPoint.EnqueueAsync(message, cancellation);
            }
            else
            {
                // TODO: Log exception
            }
        }

        #endregion

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _receiveProcess.StartAsync(cancellation);
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
            try
            {
                await _receiveProcess.TerminateAsync();
            }
            finally
            {
                IEnumerable<MultiplexEndPoint> endPoints;

                using (await _lock.LockAsync())
                {
                    endPoints = _endPoints.Values;
                }

                await Task.WhenAll(endPoints.Select(p => p.DisposeAsync()));
            }
        }

        #endregion

        private static void EncodeAddress(IMessage message, string address)
        {
            Assert(message != null);
            Assert(address != null);

            var frameIdx = message.FrameIndex;
            var addressBytes = Encoding.UTF8.GetBytes(address);

            try
            {
                using (var frameStream = message.PushFrame().OpenStream(overrideContent: true))
                using (var binaryWriter = new BinaryWriter(frameStream))
                {
                    binaryWriter.Write(addressBytes.Length);
                    binaryWriter.Write(addressBytes);
                }
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PopFrame();

                Assert(frameIdx == message.FrameIndex);

                throw;
            }
        }

        private static string DecodeAddress(IMessage message)
        {
            Assert(message != null);

            var frameIdx = message.FrameIndex;
            var address = default(string);

            try
            {
                using (var frameStream = message.PopFrame().OpenStream())
                using (var binaryReader = new BinaryReader(frameStream))
                {
                    var addressLength = binaryReader.ReadInt32();
                    var addressBytes = binaryReader.ReadBytes(addressLength);

                    address = Encoding.UTF8.GetString(addressBytes);
                }
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PushFrame();

                Assert(frameIdx == message.FrameIndex);

                throw;
            }

            return address;
        }

        public IPhysicalEndPoint<TAddress> GetMultiplexEndPoint(string address)
        {
            _logger?.LogDebug($"Retrieving multiplex end point on address '{LocalAddress}' with name '{address}'.");

            using (_disposeHelper.ProhibitDisposal())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                using (_lock.Lock())
                {
                    if (!_endPoints.TryGetValue(address, out var result))
                    {


                        result = new MultiplexEndPoint(this, address);
                        _endPoints.Add(address, result);
                    }

                    return result;
                }
            }
        }

        public async Task<IPhysicalEndPoint<TAddress>> GetMultiplexEndPointAsync(string address, CancellationToken cancellation = default)
        {
            _logger?.LogDebug($"Retrieving multiplex end point on address '{LocalAddress}' with name '{address}'.");

            using (_disposeHelper.ProhibitDisposal())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                using (await _lock.LockAsync(cancellation))
                {
                    if (!_endPoints.TryGetValue(address, out var result))
                    {
                        _logger?.LogDebug($"Creating multiplex end point on address '{LocalAddress}' with name '{address}'.");

                        result = new MultiplexEndPoint(this, address);
                        _endPoints.Add(address, result);
                    }

                    return result;
                }
            }
        }

        private sealed class MultiplexEndPoint : IPhysicalEndPoint<TAddress>
        {
            private static int _next = 1;

            private readonly int _id;

            private readonly AsyncDisposeHelper _disposeHelper;
            private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
            private readonly EndPointMultiplexer<TAddress> _multiplexer;
            private readonly string _address;

            public MultiplexEndPoint(EndPointMultiplexer<TAddress> multiplexer, string address)
            {
                Assert(multiplexer != null);
                Assert(address != null);

                _id = Interlocked.Increment(ref _next);

                _multiplexer = multiplexer;
                _address = address;
                LocalAddress = _multiplexer.LocalAddress;

                _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);

                _multiplexer._logger?.LogDebug($"[{_id}] Creating multiplex end point on address '{LocalAddress}' with name '{address}'.");
            }

            public TAddress LocalAddress { get; }

            public Task EnqueueAsync(IMessage message, CancellationToken cancellation)
            {
                Assert(message != null);

                return _rxQueue.EnqueueAsync(message, cancellation);
            }

            public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
            {
                using (await _multiplexer._disposeHelper.ProhibitDisposalAsync(cancellation))
                {
                    if (_multiplexer._disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(_multiplexer.GetType().FullName);

                    using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
                    {
                        if (_disposeHelper.IsDisposed)
                            throw new ObjectDisposedException(GetType().FullName);

                        var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_multiplexer._disposeHelper.DisposalRequested,
                                                                                                         _disposeHelper.DisposalRequested,
                                                                                                         cancellation);

                        try
                        {
                            var result = await _rxQueue.DequeueAsync(combinedCancellationSource.Token);

                            _multiplexer._logger?.LogDebug($"[{_id}] Multiplex end point on '{LocalAddress}' with name '{_address}': Received message..");

                            return result;
                        }
                        catch (OperationCanceledException exc) when (_multiplexer._disposeHelper.DisposalRequested.IsCancellationRequested)
                        {
                            throw new ObjectDisposedException(_multiplexer.GetType().FullName, exc);
                        }
                        catch (OperationCanceledException exc) when (_disposeHelper.DisposalRequested.IsCancellationRequested)
                        {
                            throw new ObjectDisposedException(GetType().FullName, exc);
                        }
                    }
                }
            }

            public async Task SendAsync(IMessage message, TAddress address, CancellationToken cancellation)
            {
                _multiplexer._logger?.LogDebug($"[{_id}] Multiplex end point on '{LocalAddress}' with name '{_address}': Sending message to remote '{address}'.");

                using (await _multiplexer._disposeHelper.ProhibitDisposalAsync(cancellation))
                {
                    if (_multiplexer._disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(_multiplexer.GetType().FullName);

                    using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
                    {
                        if (_disposeHelper.IsDisposed)
                            throw new ObjectDisposedException(GetType().FullName);

                        var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_multiplexer._disposeHelper.DisposalRequested,
                                                                                                         _disposeHelper.DisposalRequested,
                                                                                                         cancellation);

                        try
                        {
                            var frameIdx = message.FrameIndex;
                            EncodeAddress(message, _address);

                            try
                            {
                                await _multiplexer._physicalEndPoint.SendAsync(message, address, combinedCancellationSource.Token);
                            }
                            catch when (frameIdx != message.FrameIndex)
                            {
                                message.PopFrame();
                                Assert(frameIdx == message.FrameIndex);
                                throw;
                            }
                        }
                        catch (OperationCanceledException exc) when (_multiplexer._disposeHelper.DisposalRequested.IsCancellationRequested)
                        {
                            throw new ObjectDisposedException(_multiplexer.GetType().FullName, exc);
                        }
                        catch (OperationCanceledException exc) when (_disposeHelper.DisposalRequested.IsCancellationRequested)
                        {
                            throw new ObjectDisposedException(GetType().FullName, exc);
                        }
                    }
                }
            }

            #region Disposal

            public bool IsDisposed => _disposeHelper.IsDisposed;

            public Task Disposal => _disposeHelper.Disposal;

            public void Dispose()
            {
                _multiplexer._logger?.LogDebug($"[{_id}] Requesting disposing multiplex end point on address '{LocalAddress}' with name '{_address}'.");

                _disposeHelper.Dispose();
            }

            public Task DisposeAsync()
            {
                _multiplexer._logger?.LogDebug($"[{_id}] Requesting disposing multiplex end point on address '{LocalAddress}' with name '{_address}'.");

                return _disposeHelper.DisposeAsync();
                //return Task.CompletedTask;
            }

            private async Task DisposeInternalAsync()
            {
                _multiplexer._logger?.LogDebug($"[{_id}] Disposing multiplex end point on address '{LocalAddress}' with name '{_address}'.");

                using (await _multiplexer._lock.LockAsync())
                {
                    if (_multiplexer._endPoints.TryGetValue(_address, out var comparand) && comparand == this)
                    {
                        _multiplexer._endPoints.Remove(_address);
                    }
                }
            }

            #endregion
        }
    }
}
