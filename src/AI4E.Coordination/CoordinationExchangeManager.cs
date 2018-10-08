using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

namespace AI4E.Coordination
{
    internal sealed class CoordinationExchangeManager<TAddress> : ICoordinationExchangeManager<TAddress>
    {
        #region Fields

        private readonly IProvider<ICoordinationManager> _coordinationManager;
        private readonly ISessionManager _sessionManager;
        private readonly IProvider<ICoordinationWaitManager> _waitManager;
        private readonly IProvider<ICoordinationLockManager> _lockManager;
        private readonly ICoordinationStorage _storage;
        private readonly CoordinationEntryCache _cache;
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private readonly ILogger<CoordinationExchangeManager<TAddress>> _logger;

        private readonly CoordinationManagerOptions _options;
        private readonly IAsyncProcess _receiveProcess;
        private readonly DisposableAsyncLazy<IPhysicalEndPoint<TAddress>> _physicalEndPoint;

        #endregion

        #region C'tor

        public CoordinationExchangeManager(IProvider<ICoordinationManager> coordinationManager,
                                           ISessionManager sessionManager,
                                           IProvider<ICoordinationWaitManager> waitManager,
                                           IProvider<ICoordinationLockManager> lockManager,
                                           ICoordinationStorage storage,
                                           CoordinationEntryCache cache,
                                           IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                                           IAddressConversion<TAddress> addressConversion,
                                           IOptions<CoordinationManagerOptions> optionsAccessor,
                                           ILogger<CoordinationExchangeManager<TAddress>> logger = null)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (waitManager == null)
                throw new ArgumentNullException(nameof(waitManager));

            if (lockManager == null)
                throw new ArgumentNullException(nameof(lockManager));

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _coordinationManager = coordinationManager;
            _sessionManager = sessionManager;
            _waitManager = waitManager;
            _lockManager = lockManager;
            _storage = storage;
            _cache = cache;
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

        public ICoordinationManager CoordinationManager => _coordinationManager.ProvideInstance();

        private async Task<IPhysicalEndPoint<TAddress>> GetLocalSessionEndPointAsync(CancellationToken cancellation)
        {
            var session = await CoordinationManager.GetSessionAsync(cancellation);
            return GetSessionEndPoint(session);
        }

        private Task DisposePhysicalEndPointAsync(IPhysicalEndPoint<TAddress> physicalEndPoint)
        {
            return physicalEndPoint.Assert(p => p != null)
                                   .DisposeIfDisposableAsync()
                                   .HandleExceptionsAsync(_logger);
        }

        #endregion

        private ICoordinationWaitManager WaitManager => _waitManager.ProvideInstance();
        private ICoordinationLockManager LockManager => _lockManager.ProvideInstance();

        #region ICoordinationExchangeManager

        public ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<IPhysicalEndPoint<TAddress>>(_physicalEndPoint.Task.WithCancellation(cancellation));
        }

        public async Task NotifyReadLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var sessions = _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await CoordinationManager.GetSessionAsync(cancellation);

            var enumerator = sessions.GetEnumerator();
            try
            {
                while (await enumerator.MoveNext(cancellation))
                {
                    var session = enumerator.Current;

                    if (session == localSession)
                    {
                        WaitManager.NotifyReadLockRelease(path, session);
                        continue;
                    }

                    // The session is the former read-lock owner.
                    var message = EncodeMessage(MessageType.ReleasedReadLock, path, localSession);

                    await SendMessageAsync(session, message, cancellation);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public async Task NotifyWriteLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var sessions = _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await CoordinationManager.GetSessionAsync(cancellation);

            var enumerator = sessions.GetEnumerator();
            try
            {
                while (await enumerator.MoveNext(cancellation))
                {
                    var session = enumerator.Current;

                    if (session == localSession)
                    {
                        WaitManager.NotifyWriteLockRelease(path, session);
                        continue;
                    }

                    // The session is the former write-lock owner.
                    var message = EncodeMessage(MessageType.ReleasedWriteLock, path, localSession);

                    await SendMessageAsync(session, message, cancellation);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public async Task InvalidateCacheEntryAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            if (session == await CoordinationManager.GetSessionAsync(cancellation))
            {
                await InvalidateCacheEntryAsync(path, cancellation);
            }
            else
            {
                // The session is the read-lock owner (It caches the entry currently)
                var message = EncodeMessage(MessageType.InvalidateCacheEntry, path, session);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        public void Dispose()
        {
            _receiveProcess.Terminate();
            _physicalEndPoint.Dispose();
        }

        #endregion

        private async Task InvalidateCacheEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);

            if (!cacheEntry.TryGetEntry(out var entry))
            {
                entry = await _storage.GetEntryAsync(path, cancellation);
            }

            _cache.InvalidateEntry(path);
            await LockManager.ReleaseReadLockAsync(entry, cancellation);
        }

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            var physicalEndPoint = await GetPhysicalEndPointAsync(cancellation);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, _) = await physicalEndPoint.ReceiveAsync(cancellation);
                    var (messageType, path, session) = DecodeMessage(message);

                    Task.Run(() => HandleMessageAsync(message, messageType, path, session, cancellation)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{await CoordinationManager.GetSessionAsync(cancellation)}] Failure while decoding received message.");
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message, MessageType messageType, CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            switch (messageType)
            {
                case MessageType.InvalidateCacheEntry:
                    if (session != await CoordinationManager.GetSessionAsync(cancellation))
                    {
                        _logger?.LogWarning($"[{await CoordinationManager.GetSessionAsync(cancellation)}] Received invalidate message for session that is not present.");
                    }
                    else
                    {
                        await InvalidateCacheEntryAsync(path, cancellation);
                    }
                    break;

                case MessageType.ReleasedReadLock:
                    WaitManager.NotifyReadLockRelease(path, session);
                    break;

                case MessageType.ReleasedWriteLock:
                    WaitManager.NotifyWriteLockRelease(path, session);
                    break;

                case MessageType.Unknown:
                default:
                    _logger?.LogWarning($"[{await CoordinationManager.GetSessionAsync(cancellation)}] Received invalid message or message with unknown message type.");
                    break;
            }
        }

        private async Task SendMessageAsync(Session session, Message message, CancellationToken cancellation)
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

        private IPhysicalEndPoint<TAddress> GetSessionEndPoint(Session session)
        {
            Assert(session != null);
            var multiplexName = GetMultiplexEndPointName(session);
            return _endPointMultiplexer.GetPhysicalEndPoint(multiplexName)
                                       .Assert(p => p != null);
        }

        private string GetMultiplexEndPointName(Session session)
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

        private (MessageType messageType, CoordinationEntryPath path, Session session) DecodeMessage(IMessage message)
        {
            Assert(message != null);

            using (var frameStream = message.PopFrame().OpenStream())
            using (var binaryReader = new BinaryReader(frameStream))
            {
                var messageType = (MessageType)binaryReader.ReadByte();

                var escapedPath = binaryReader.ReadUtf8();
                var path = CoordinationEntryPath.FromEscapedPath(escapedPath);

                var sessionLength = binaryReader.ReadInt32();
                var sessionBytes = binaryReader.ReadBytes(sessionLength);
                var session = Session.FromChars(Encoding.UTF8.GetString(sessionBytes).AsSpan());

                return (messageType, path, session);
            }
        }

        private Message EncodeMessage(MessageType messageType, CoordinationEntryPath path, Session session)
        {
            var message = new Message();

            EncodeMessage(message, messageType, path, session);

            return message;
        }

        private void EncodeMessage(IMessage message, MessageType messageType, CoordinationEntryPath path, Session session)
        {
            Assert(message != null);
            // Modify if other message types are added
            Assert(messageType >= MessageType.InvalidateCacheEntry && messageType <= MessageType.ReleasedWriteLock);

            using (var frameStream = message.PushFrame().OpenStream())
            using (var binaryWriter = new BinaryWriter(frameStream))
            {
                binaryWriter.Write((byte)messageType);

                binaryWriter.WriteUtf8(path.EscapedPath.Span);

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
