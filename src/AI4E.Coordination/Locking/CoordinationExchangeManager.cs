using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Coordination.Storage;
using AI4E.Remoting;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;
using static AI4E.Utils.DebugEx;

namespace AI4E.Coordination.Locking
{
    public sealed class CoordinationExchangeManager<TAddress> : ICoordinationExchangeManager<TAddress>
    {
        #region Fields

        private readonly ICoordinationSessionOwner _sessionOwner;
        private readonly ISessionManager _sessionManager;
        private readonly ILockWaitDirectory _lockWaitDirectory;
        private readonly IInvalidationCallbackDirectory _invalidationCallbackDirectory;
        private readonly ICoordinationStorage _storage;
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private readonly ILogger<CoordinationExchangeManager<TAddress>> _logger;

        private readonly CoordinationManagerOptions _options;
        private readonly IAsyncProcess _receiveProcess;
        private readonly DisposableAsyncLazy<IPhysicalEndPoint<TAddress>> _physicalEndPoint;

        #endregion

        #region C'tor

        public CoordinationExchangeManager(ICoordinationSessionOwner sessionOwner,
                                           ISessionManager sessionManager,
                                           ILockWaitDirectory lockWaitDirectory,
                                           IInvalidationCallbackDirectory invalidationCallbackDirectory,
                                           ICoordinationStorage storage,
                                           IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                                           IAddressConversion<TAddress> addressConversion,
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

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _sessionOwner = sessionOwner;
            _sessionManager = sessionManager;
            _lockWaitDirectory = lockWaitDirectory;
            _invalidationCallbackDirectory = invalidationCallbackDirectory;
            _storage = storage;
            _endPointMultiplexer = endPointMultiplexer;
            _addressConversion = addressConversion;
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
            var session = await _sessionOwner.GetSessionAsync(cancellation);
            return GetSessionEndPoint(session);
        }

        private Task DisposePhysicalEndPointAsync(IPhysicalEndPoint<TAddress> physicalEndPoint)
        {
            return physicalEndPoint.Assert(p => p != null)
                                   .DisposeIfDisposableAsync()
                                   .HandleExceptionsAsync(_logger);
        }

        #endregion

        #region ICoordinationExchangeManager

        public ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<IPhysicalEndPoint<TAddress>>(_physicalEndPoint.Task.WithCancellation(cancellation));
        }

        public async ValueTask NotifyReadLockReleasedAsync(string key, CancellationToken cancellation)
        {
            var sessions = _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await _sessionOwner.GetSessionAsync(cancellation);

            var enumerator = sessions.GetEnumerator();
            try
            {
                while (await enumerator.MoveNext(cancellation))
                {
                    var session = enumerator.Current;

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
            finally
            {
                enumerator.Dispose();
            }
        }

        public async ValueTask NotifyWriteLockReleasedAsync(string key, CancellationToken cancellation)
        {
            var sessions = _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await _sessionOwner.GetSessionAsync(cancellation);

            var enumerator = sessions.GetEnumerator();
            try
            {
                while (await enumerator.MoveNext(cancellation))
                {
                    var session = enumerator.Current;

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
            finally
            {
                enumerator.Dispose();
            }
        }

        public async ValueTask InvalidateCacheEntryAsync(string key, CoordinationSession session, CancellationToken cancellation)
        {
            if (session == await _sessionOwner.GetSessionAsync(cancellation))
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

        public void Dispose()
        {
            _receiveProcess.Terminate();
            _physicalEndPoint.Dispose();
        }

        #endregion

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            var physicalEndPoint = await GetPhysicalEndPointAsync(cancellation);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, _) = await physicalEndPoint.ReceiveAsync(cancellation);
                    var (messageType, key, session) = DecodeMessage(message);

                    Task.Run(() => HandleMessageAsync(message, messageType, key, session, cancellation)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{await _sessionOwner.GetSessionAsync(cancellation)}] Failure while decoding received message.");
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message, MessageType messageType, string key, CoordinationSession session, CancellationToken cancellation)
        {
            switch (messageType)
            {
                case MessageType.InvalidateCacheEntry:
                    if (session != await _sessionOwner.GetSessionAsync(cancellation))
                    {
                        _logger?.LogWarning($"[{await _sessionOwner.GetSessionAsync(cancellation)}] Received invalidate message for session that is not present.");
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
                    _logger?.LogWarning($"[{await _sessionOwner.GetSessionAsync(cancellation)}] Received invalid message or message with unknown message type.");
                    break;
            }
        }

        private async Task SendMessageAsync(CoordinationSession session, Message message, CancellationToken cancellation)
        {
            var remoteAddress = _addressConversion.DeserializeAddress(session.PhysicalAddress.ToArray()); // TODO: This will copy everything to a new aray

            Assert(remoteAddress != null);

            var physicalEndPoint = GetSessionEndPoint(session);

            try
            {
                await physicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
            catch (SocketException) { }
            catch (IOException) { } // The remote session terminated or we just cannot transmit to it.

        }

        private IPhysicalEndPoint<TAddress> GetSessionEndPoint(CoordinationSession session)
        {
            Assert(session != null);
            var multiplexName = GetMultiplexEndPointName(session);
            return _endPointMultiplexer.GetPhysicalEndPoint(multiplexName)
                                       .Assert(p => p != null);
        }

        private string GetMultiplexEndPointName(CoordinationSession session)
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

        private (MessageType messageType, string key, CoordinationSession session) DecodeMessage(IMessage message)
        {
            Assert(message != null);

            using (var frameStream = message.PopFrame().OpenStream())
            using (var binaryReader = new BinaryReader(frameStream))
            {
                var messageType = (MessageType)binaryReader.ReadByte();

                var key = binaryReader.ReadString();
                var sessionLength = binaryReader.ReadInt32();
                var sessionBytes = binaryReader.ReadBytes(sessionLength);
                var session = CoordinationSession.FromChars(Encoding.UTF8.GetString(sessionBytes).AsSpan());

                return (messageType, key, session);
            }
        }

        private Message EncodeMessage(MessageType messageType, string key, CoordinationSession session)
        {
            var message = new Message();

            EncodeMessage(message, messageType, key, session);

            return message;
        }

        private void EncodeMessage(IMessage message, MessageType messageType, string key, CoordinationSession session)
        {
            Assert(message != null);
            // Modify if other message types are added
            Assert(messageType >= MessageType.InvalidateCacheEntry && messageType <= MessageType.ReleasedWriteLock);

            using (var frameStream = message.PushFrame().OpenStream())
            using (var binaryWriter = new BinaryWriter(frameStream))
            {
                binaryWriter.Write((byte)messageType);
                binaryWriter.Write(key);

                var sessionBytes = Encoding.UTF8.GetBytes(session.ToString());
                binaryWriter.Write(sessionBytes.Length);
                binaryWriter.Write(sessionBytes);
            }
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
