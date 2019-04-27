/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using AI4E.Utils;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Remoting
{
    /// <summary>
    /// Multiplexes a single physical end point by to multiple end points each distinguished by a multiplex name.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// This type is not meant to be consumed directly but is part of the infrastructure to enable the remote message dispatching system.
    /// </remarks>
    public sealed class PhysicalEndPointMultiplexer<TAddress> : IPhysicalEndPointMultiplexer<TAddress>
    {
        #region Fields

        private readonly IPhysicalEndPoint<TAddress> _physicalEndPoint;
        private readonly ILogger<PhysicalEndPointMultiplexer<TAddress>> _logger;
        private readonly WeakDictionary<string, AsyncProducerConsumerQueue<(IMessage message, TAddress remoteAddress)>> _rxQueues;
        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncDisposeHelper _disposeHelper;

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="IMultiplexPhysicalEndPoint{TAddress}"/> type.
        /// </summary>
        /// <param name="physicalEndPoint">The underlying physical end point.</param>
        /// <param name="logger">A logger used to log messages.</param>
        public PhysicalEndPointMultiplexer(IPhysicalEndPoint<TAddress> physicalEndPoint, ILogger<PhysicalEndPointMultiplexer<TAddress>> logger = null)
        {
            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            _physicalEndPoint = physicalEndPoint;
            _logger = logger;

            _rxQueues = new WeakDictionary<string, AsyncProducerConsumerQueue<(IMessage message, TAddress remoteAddress)>>();
            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
            _disposeHelper = new AsyncDisposeHelper(_receiveProcess.TerminateAsync);
        }

        #endregion

        #region AddressConversion

        /// <inheritdoc />
        public string AddressToString(TAddress address)
        {
            return _physicalEndPoint.AddressToString(address);
        }

        /// <inheritdoc />
        public TAddress AddressFromString(string str)
        {
            return _physicalEndPoint.AddressFromString(str);
        }

        #endregion

        /// <summary>
        /// Gets the physical address of the underlying local physical end point.
        /// </summary>
        public TAddress LocalAddress => _physicalEndPoint.LocalAddress;

        #region ReceiveProcess

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, remoteAddress) = await _physicalEndPoint.ReceiveAsync(cancellation);

                    Assert(message != null);
                    Assert(remoteAddress != null);

                    HandleMessageAsync(message, remoteAddress, cancellation).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, "An incoming message could not be processed.");
                }
            }
        }

        private Task HandleMessageAsync(IMessage message, TAddress remoteAddress, CancellationToken cancellation)
        {
            Assert(message != null);

            var multiplexName = DecodeMultiplexName(message);
            return GetRxQueue(multiplexName).EnqueueAsync((message, remoteAddress), cancellation);
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Disposes of the type.
        /// </summary>
        /// <remarks>
        /// This method does not block but instead only initiates the disposal without actually waiting till disposal is completed.
        /// </remarks>
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        #endregion

        private AsyncProducerConsumerQueue<(IMessage message, TAddress remoteAddress)> GetRxQueue(string multiplexName)
        {
            var result = _rxQueues.GetOrAdd(multiplexName, _ => new AsyncProducerConsumerQueue<(IMessage message, TAddress remoteAddress)>());

            Assert(result != null);

            return result;
        }

        #region Coding

        private static void EncodeMultiplexName(IMessage message, string multiplexName)
        {
            Assert(message != null);
            Assert(multiplexName != null);

            var frameIdx = message.FrameIndex;
            var multiplexNameBytes = Encoding.UTF8.GetBytes(multiplexName);

            try
            {
                using (var frameStream = message.PushFrame().OpenStream(overrideContent: true))
                using (var binaryWriter = new BinaryWriter(frameStream))
                {
                    binaryWriter.Write(multiplexNameBytes.Length);
                    binaryWriter.Write(multiplexNameBytes);
                }
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PopFrame();

                Assert(frameIdx == message.FrameIndex);

                throw;
            }
        }

        private static string DecodeMultiplexName(IMessage message)
        {
            Assert(message != null);

            var frameIdx = message.FrameIndex;
            var result = default(string);

            try
            {
                using (var frameStream = message.PopFrame().OpenStream())
                using (var binaryReader = new BinaryReader(frameStream))
                {
                    var multiplexNameLength = binaryReader.ReadInt32();
                    var multiplexNameBytes = binaryReader.ReadBytes(multiplexNameLength);

                    result = Encoding.UTF8.GetString(multiplexNameBytes);
                }
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PushFrame();

                Assert(frameIdx == message.FrameIndex);

                throw;
            }

            Assert(result != null);

            return result;
        }

        #endregion

        /// <summary>
        /// Returns a physical end point that is identified by the specified multiplex name.
        /// </summary>
        /// <param name="multiplexName">The name of the multiplex end point.</param>
        /// <returns>A physical end point identified by <paramref name="multiplexName"/>.</returns>
        /// <exception cref="ArgumentNullOrWhiteSpaceException">Thrown if <paramref name="multiplexName"/> is either null, an emppty string or contains of whitespace only.</exception>
        public IMultiplexPhysicalEndPoint<TAddress> GetPhysicalEndPoint(string multiplexName)
        {
            if (string.IsNullOrWhiteSpace(multiplexName))
                throw new ArgumentNullOrWhiteSpaceException(nameof(multiplexName));

            return new MultiplexPhysicalEndPoint(this, multiplexName);
        }

        private sealed class MultiplexPhysicalEndPoint : IMultiplexPhysicalEndPoint<TAddress>
        {
            // This must be stored as field and not looked up dynamically in ReceiveAsync
            // in order that the Multiplexer does not delete the collection.
            private readonly AsyncProducerConsumerQueue<(IMessage message, TAddress remoteAddress)> _rxQueue;
            private readonly PhysicalEndPointMultiplexer<TAddress> _multiplexer;

            public MultiplexPhysicalEndPoint(PhysicalEndPointMultiplexer<TAddress> multiplexer, string multiplexName)
            {
                if (multiplexer == null)
                    throw new ArgumentNullException(nameof(multiplexer));

                _multiplexer = multiplexer;
                MultiplexName = multiplexName;

                _rxQueue = _multiplexer.GetRxQueue(MultiplexName);
            }

            #region AddressConversion

            /// <inheritdoc />
            public string AddressToString(TAddress address)
            {
                return _multiplexer.AddressToString(address);
            }

            /// <inheritdoc />
            public TAddress AddressFromString(string str)
            {
                return _multiplexer.AddressFromString(str);
            }

            #endregion

            public TAddress LocalAddress => _multiplexer.LocalAddress;

            public string MultiplexName { get; }

            public async Task<(IMessage message, TAddress remoteAddress)> ReceiveAsync(CancellationToken cancellation = default)
            {
                try
                {
                    using (var guard = _multiplexer._disposeHelper.GuardDisposal(cancellation))
                    {
                        return await _rxQueue.DequeueAsync(guard.Cancellation);
                    }
                }
                catch (OperationCanceledException) when (_multiplexer._disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(_multiplexer.GetType().FullName);
                }
            }

            public async Task SendAsync(IMessage message, TAddress remoteAddress, CancellationToken cancellation = default)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (remoteAddress == null)
                    throw new ArgumentNullException(nameof(remoteAddress));

                if (remoteAddress.Equals(default))
                    throw new ArgumentDefaultException(nameof(message));

                try
                {
                    using (var guard = _multiplexer._disposeHelper.GuardDisposal(cancellation))
                    {
                        await SendInternalAsync(message, remoteAddress, guard.Cancellation);
                    }
                }
                catch (OperationCanceledException) when (_multiplexer._disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(_multiplexer.GetType().FullName);
                }
            }

            private async Task SendInternalAsync(IMessage message, TAddress address, CancellationToken cancellation)
            {
                var frameIdx = message.FrameIndex;
                EncodeMultiplexName(message, MultiplexName);

                try
                {
                    await _multiplexer._physicalEndPoint.SendAsync(message, address, cancellation);
                }
                catch when (frameIdx != message.FrameIndex)
                {
                    message.PopFrame();
                    Assert(frameIdx == message.FrameIndex);
                    throw;
                }
            }

            public void Dispose() { }
        }
    }
}
