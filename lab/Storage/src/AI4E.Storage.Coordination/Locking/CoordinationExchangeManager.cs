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
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Storage.Coordination.Session;
using AI4E.Utils.Async;
using AI4E.Utils.Messaging.Primitives;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Coordination.Locking
{
    /// <summary>
    /// Manages the communication of coordination coordination service sessions..
    /// </summary>
    /// <typeparam name="TAddress">
    /// The type of address the messaging system uses.
    /// </typeparam>
    public sealed class CoordinationExchangeManager<TAddress> : ICoordinationExchangeManager<TAddress>
    {
        #region Fields

        private readonly ISessionOwner _sessionOwner;
        private readonly ISessionManager _sessionManager;
        private readonly ILockWaitDirectory _lockWaitDirectory;
        private readonly IInvalidationCallbackDirectory _invalidationCallbackDirectory;
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly ILogger<CoordinationExchangeManager<TAddress>> _logger;

        private readonly CoordinationManagerOptions _options;
        private readonly IAsyncProcess _receiveProcess;
        private readonly DisposableAsyncLazy<IPhysicalEndPoint<TAddress>> _physicalEndPoint;

        #endregion

        #region C'tor

        /// <summary>
        /// Create a new instance of the <see cref="CoordinationExchangeManager{TAddress}"/>
        /// type.
        /// </summary>
        /// <param name="sessionOwner">
        /// A <see cref="ISessionOwner"/> that is used to retrieve the current session.
        /// </param>
        /// <param name="sessionManager">
        /// A <see cref="ISessionManager"/> that is used to manage coordination service sessions.
        /// </param>
        /// <param name="lockWaitDirectory">
        /// A <see cref="ILockWaitDirectory"/> that is used to notify of released locks.
        /// </param>
        /// <param name="invalidationCallbackDirectory">
        /// A <see cref="IInvalidationCallbackDirectory"/> that is used to invalidate entries.
        /// </param>
        /// <param name="endPointMultiplexer">
        /// An <see cref="IPhysicalEndPointMultiplexer{TAddress}"/> to communicate with other
        /// coordination service sessions.
        /// </param>
        /// <param name="optionsAccessor">
        /// An <see cref="IOptions{TOptions}"/> that is used to access the <see cref="CoordinationManagerOptions"/>.
        /// </param>
        /// <param name="logger">A <see cref="Logger{T}"/> that is used to log messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="sessionOwner"/>, <paramref name="sessionManager"/>,
        /// <paramref name="lockWaitDirectory"/>, <paramref name="invalidationCallbackDirectory"/>,
        /// <paramref name="endPointMultiplexer"/> or <paramref name="optionsAccessor"/> is <c>null</c>.
        /// </exception>
        public CoordinationExchangeManager(
            ISessionOwner sessionOwner,
            ISessionManager sessionManager,
            ILockWaitDirectory lockWaitDirectory,
            IInvalidationCallbackDirectory invalidationCallbackDirectory,
            IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
            IOptions<CoordinationManagerOptions> optionsAccessor,
            ILogger<CoordinationExchangeManager<TAddress>> logger = null)
        {
            if (sessionOwner == null)
                throw new ArgumentNullException(nameof(sessionOwner));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (lockWaitDirectory == null)
                throw new ArgumentNullException(nameof(lockWaitDirectory));

            if (invalidationCallbackDirectory == null)
                throw new ArgumentNullException(nameof(invalidationCallbackDirectory));

            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            _sessionOwner = sessionOwner;
            _sessionManager = sessionManager;
            _lockWaitDirectory = lockWaitDirectory;
            _invalidationCallbackDirectory = invalidationCallbackDirectory;
            _endPointMultiplexer = endPointMultiplexer;
            _logger = logger;

            _options = optionsAccessor.Value ?? new CoordinationManagerOptions();
            _physicalEndPoint = new DisposableAsyncLazy<IPhysicalEndPoint<TAddress>>(
                factory: GetLocalSessionEndPointAsync,
                disposal: DisposePhysicalEndPointAsync,
                DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);

            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _receiveProcess.Start();
        }

        private async Task<IPhysicalEndPoint<TAddress>> GetLocalSessionEndPointAsync(CancellationToken cancellation)
        {
            var session = await _sessionOwner.GetSessionIdentifierAsync(cancellation);
            return GetSessionEndPoint(session);
        }

        private Task DisposePhysicalEndPointAsync(IPhysicalEndPoint<TAddress> physicalEndPoint)
        {
            if (physicalEndPoint is null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            return physicalEndPoint.DisposeIfDisposableAsync().HandleExceptionsAsync(_logger).AsTask();
        }

        #endregion

        #region ICoordinationExchangeManager

        /// <inheritdoc />
        public ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<IPhysicalEndPoint<TAddress>>(_physicalEndPoint.Task.WithCancellation(cancellation));
        }

        /// <inheritdoc />
        public async ValueTask NotifyReadLockReleasedAsync(string key, CancellationToken cancellation)
        {
            var localSession = await _sessionOwner.GetSessionIdentifierAsync(cancellation);

            await foreach (var session in _sessionManager.GetSessionsAsync(cancellation))
            {
                if (session == localSession)
                {
                    _lockWaitDirectory.NotifyReadLockRelease(key, session);
                    continue;
                }

                // The session is the former read-lock owner.
                var message = EncodeMessage(MessageType.ReleasedReadLock, key, localSession);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        /// <inheritdoc />
        public async ValueTask NotifyWriteLockReleasedAsync(string key, CancellationToken cancellation)
        {
            var localSession = await _sessionOwner.GetSessionIdentifierAsync(cancellation);

            await foreach (var session in _sessionManager.GetSessionsAsync(cancellation))
            {
                if (session == localSession)
                {
                    _lockWaitDirectory.NotifyWriteLockRelease(key, session);
                    continue;
                }

                // The session is the former write-lock owner.
                var message = EncodeMessage(MessageType.ReleasedWriteLock, key, localSession);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        /// <inheritdoc />
        public async ValueTask InvalidateCacheEntryAsync(string key, SessionIdentifier session, CancellationToken cancellation)
        {
            if (session == await _sessionOwner.GetSessionIdentifierAsync(cancellation))
            {
                await _invalidationCallbackDirectory.InvokeAsync(key, cancellation);
            }
            else
            {
                // The session is the read-lock owner (It caches the entry currently)
                var message = EncodeMessage(MessageType.InvalidateCacheEntry, key, session);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _receiveProcess.Terminate();
            _physicalEndPoint.Dispose();
        }

        #endregion

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            var physicalEndPoint = await GetPhysicalEndPointAsync(cancellation); // TODO: Do we need to dispose this?

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var transmission = await physicalEndPoint.ReceiveAsync(cancellation);
                    var (messageType, key, session) = DecodeMessage(transmission.Message);

                    Task.Run(() => HandleMessageAsync(messageType, key, session, cancellation)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{await _sessionOwner.GetSessionIdentifierAsync(cancellation)}] Failure while decoding received message.");
                }
            }
        }

        private async Task HandleMessageAsync(
            MessageType messageType, string key, SessionIdentifier session, CancellationToken cancellation)
        {
            switch (messageType)
            {
                case MessageType.InvalidateCacheEntry:
                    if (session != await _sessionOwner.GetSessionIdentifierAsync(cancellation))
                    {
                        _logger?.LogWarning($"[{await _sessionOwner.GetSessionIdentifierAsync(cancellation)}] Received invalidate message for session that is not present.");
                    }
                    else
                    {
                        await _invalidationCallbackDirectory.InvokeAsync(key, cancellation);
                    }
                    break;

                case MessageType.ReleasedReadLock:
                    _lockWaitDirectory.NotifyReadLockRelease(key, session);
                    break;

                case MessageType.ReleasedWriteLock:
                    _lockWaitDirectory.NotifyWriteLockRelease(key, session);
                    break;

                case MessageType.Unknown:
                default:
                    _logger?.LogWarning($"[{await _sessionOwner.GetSessionIdentifierAsync(cancellation)}] Received invalid message or message with unknown message type.");
                    break;
            }
        }

        private async Task SendMessageAsync(SessionIdentifier session, Message message, CancellationToken cancellation)
        {
            // TODO: This allocates. Can we cache this?
            var stringifiedAddress = Encoding.UTF8.GetString(session.PhysicalAddress.Span);
            var remoteAddress = _endPointMultiplexer.AddressFromString(stringifiedAddress);

            Assert(remoteAddress != null);

            var physicalEndPoint = GetSessionEndPoint(session);

            try
            {
                await physicalEndPoint.SendAsync(new Transmission<TAddress>(message, remoteAddress), cancellation);
            }
            catch (SocketException) { }
            catch (IOException) { } // The remote session terminated or we just cannot transmit to it.
        }

        private IPhysicalEndPoint<TAddress> GetSessionEndPoint(SessionIdentifier session)
        {
            Assert(session != null);
            var multiplexName = GetMultiplexEndPointName(session);
            return _endPointMultiplexer.GetPhysicalEndPoint(multiplexName);
        }

        // TODO: Cache this
        private string GetMultiplexEndPointName(SessionIdentifier session)
        {
            var prefix = _options.MultiplexPrefix;

            if (prefix == null)
            {
                prefix = CoordinationManagerOptions.MultiplexPrefixDefault;
            }

            if (prefix == string.Empty)
            {
                return session.ToString();
            }

            return prefix + session.ToString();
        }

        private (MessageType messageType, string key, SessionIdentifier session) DecodeMessage(Message message)
        {
            using var frameStream = message.PeekFrame().OpenStream();
            using var binaryReader = new BinaryReader(frameStream);

            var messageType = (MessageType)binaryReader.ReadByte();
            var key = binaryReader.ReadString();
            var sessionLength = binaryReader.ReadInt32();
            var sessionBytes = binaryReader.ReadBytes(sessionLength);
            var session = SessionIdentifier.FromChars(Encoding.UTF8.GetString(sessionBytes).AsSpan());

            return (messageType, key, session);
        }

        private Message EncodeMessage(MessageType messageType, string key, SessionIdentifier session)
        {
            var messageBuilder = new MessageBuilder();

            EncodeMessage(messageBuilder, messageType, key, session);

            return messageBuilder.BuildMessage();
        }

        private void EncodeMessage(MessageBuilder messageBuilder, MessageType messageType, string key, SessionIdentifier session)
        {
            Assert(messageBuilder != null);
            // Modify if other message types are added
            Assert(messageType >= MessageType.InvalidateCacheEntry && messageType <= MessageType.ReleasedWriteLock);

            using var frameStream = messageBuilder.PushFrame().OpenStream();
            using var binaryWriter = new BinaryWriter(frameStream);

            binaryWriter.Write((byte)messageType);
            binaryWriter.Write(key);

            var sessionBytes = Encoding.UTF8.GetBytes(session.ToString());
            binaryWriter.Write(sessionBytes.Length);
            binaryWriter.Write(sessionBytes);
        }

        private enum MessageType : byte
        {
            Unknown = 0,
            InvalidateCacheEntry = 1,
            ReleasedReadLock = 2,
            ReleasedWriteLock = 3
        }
    }
}
