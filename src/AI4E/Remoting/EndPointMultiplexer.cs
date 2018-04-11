using System;
using System.Collections.Concurrent;
using System.IO;
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
        private readonly ConcurrentDictionary<string, MultiplexEndPoint> _endPoints = new ConcurrentDictionary<string, MultiplexEndPoint>();
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

        private static void EncodeAddress(IMessage message, string address)
        {
            Assert(message != null);
            Assert(address != null);

            var frameIdx = message.FrameIndex;
            var addressBytes = Encoding.UTF32.GetBytes(address);

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

        private async Task HandleMessageAsync(IMessage message, CancellationToken cancellation)
        {
            Assert(message != null);

            var address = DecodeAddress(message);

            if (_endPoints.TryGetValue(address, out var endPoint))
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
            await _receiveProcess.TerminateAsync();
        }

        #endregion

        public IPhysicalEndPoint<TAddress> GetMultiplexEndPoint(string address)
        {
            using (_disposeHelper.ProhibitDisposal())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var result = new MultiplexEndPoint(this, address);

                if (!_endPoints.TryAdd(address, result))
                {
                    throw new Exception("End point already present."); // TODO
                }

                return result;
            }
        }

        private sealed class MultiplexEndPoint : IPhysicalEndPoint<TAddress>
        {
            private readonly EndPointMultiplexer<TAddress> _multiplexer;
            private readonly string _address;
            private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();

            public MultiplexEndPoint(EndPointMultiplexer<TAddress> multiplexer, string address)
            {
                Assert(multiplexer != null);
                Assert(address != null);

                _multiplexer = multiplexer;
                _address = address;
                LocalAddress = _multiplexer._physicalEndPoint.LocalAddress;
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

                    var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _multiplexer._disposeHelper.DisposalRequested);

                    try
                    {
                        return await _rxQueue.DequeueAsync(combinedCancellationSource.Token);
                    }
                    catch (OperationCanceledException exc) when (_multiplexer._disposeHelper.DisposalRequested.IsCancellationRequested)
                    {
                        throw new ObjectDisposedException(_multiplexer.GetType().FullName, exc);
                    }
                }
            }

            public async Task SendAsync(IMessage message, TAddress address, CancellationToken cancellation)
            {
                using (await _multiplexer._disposeHelper.ProhibitDisposalAsync(cancellation))
                {
                    if (_multiplexer._disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(_multiplexer.GetType().FullName);

                    var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _multiplexer._disposeHelper.DisposalRequested);

                    try
                    {
                        // TODO: If any of the following fails, the message may be corrupted. Correct this.
                        EncodeAddress(message, _address);

                        await _multiplexer._physicalEndPoint.SendAsync(message, address, combinedCancellationSource.Token);
                    }
                    catch (OperationCanceledException exc) when (_multiplexer._disposeHelper.DisposalRequested.IsCancellationRequested)
                    {
                        throw new ObjectDisposedException(_multiplexer.GetType().FullName, exc);
                    }
                }
            }
        }
    }
}
