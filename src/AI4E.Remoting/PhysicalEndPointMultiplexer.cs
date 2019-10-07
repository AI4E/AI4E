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
using AI4E.Utils.Messaging.Primitives;

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
        private readonly WeakDictionary<string, AsyncProducerConsumerQueue<Transmission<TAddress>>> _rxQueues;
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

            _rxQueues = new WeakDictionary<string, AsyncProducerConsumerQueue<Transmission<TAddress>>>();
            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
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
                    var transmission = await _physicalEndPoint.ReceiveAsync(cancellation);

                    HandleMessageAsync(transmission, cancellation).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, "An incoming message could not be processed.");
                }
            }
        }

        private Task HandleMessageAsync(Transmission<TAddress> transmission, CancellationToken cancellation)
        {
            var (message, multiplexName) = DecodeMultiplexName(transmission.Message);
            return GetRxQueue(multiplexName).EnqueueAsync(new Transmission<TAddress>(message, transmission.RemoteAddress), cancellation);
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

        private ValueTask DisposeInternalAsync()
        {
            return _receiveProcess.TerminateAsync().AsValueTask();
        }

        #endregion

        private AsyncProducerConsumerQueue<Transmission<TAddress>> GetRxQueue(string multiplexName)
        {
            var result = _rxQueues.GetOrAdd(multiplexName, _ => new AsyncProducerConsumerQueue<Transmission<TAddress>>());

            Assert(result != null);

            return result;
        }

        #region Coding

        private static Message EncodeMultiplexName(Message message, string multiplexName)
        {
            Assert(multiplexName != null);

            var multiplexNameBytes = Encoding.UTF8.GetBytes(multiplexName);
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var binaryWriter = new BinaryWriter(frameStream))
            {
                binaryWriter.Write(multiplexNameBytes.Length);
                binaryWriter.Write(multiplexNameBytes);
            }

            return message.PushFrame(frameBuilder.BuildMessageFrame());

        }

        private static (Message message, string multiplexName) DecodeMultiplexName(Message message)
        {
            var multiplexName = default(string);

            message = message.PopFrame(out var frame);

            using (var frameStream = frame.OpenStream())
            using (var binaryReader = new BinaryReader(frameStream))
            {
                var multiplexNameLength = binaryReader.ReadInt32();
                var multiplexNameBytes = binaryReader.ReadBytes(multiplexNameLength);

                multiplexName = Encoding.UTF8.GetString(multiplexNameBytes);
            }

            Assert(multiplexName != null);

            return (message, multiplexName);
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
            private readonly AsyncProducerConsumerQueue<Transmission<TAddress>> _rxQueue;
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

            public async ValueTask<Transmission<TAddress>> ReceiveAsync(CancellationToken cancellation = default)
            {
                try
                {
                    using var guard = _multiplexer._disposeHelper.GuardDisposal(cancellation);
                    return await _rxQueue.DequeueAsync(guard.Cancellation);
                }
                catch (OperationCanceledException) when (_multiplexer._disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(_multiplexer.GetType().FullName);
                }
            }

            public async ValueTask SendAsync(Transmission<TAddress> transmission, CancellationToken cancellation = default)
            {
                if (transmission.Equals(default)) // TODO: Use ==
                    throw new ArgumentDefaultException(nameof(transmission));

                try
                {
                    using var guard = _multiplexer._disposeHelper.GuardDisposal(cancellation);
                    await SendInternalAsync(transmission, guard.Cancellation);
                }
                catch (OperationCanceledException) when (_multiplexer._disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(_multiplexer.GetType().FullName);
                }
            }

            private async ValueTask SendInternalAsync(Transmission<TAddress> transmission, CancellationToken cancellation)
            {
                var message = EncodeMultiplexName(transmission.Message, MultiplexName);
                await _multiplexer._physicalEndPoint.SendAsync(new Transmission<TAddress>(message, transmission.RemoteAddress), cancellation);
            }

            public void Dispose() { }
        }
    }
}
