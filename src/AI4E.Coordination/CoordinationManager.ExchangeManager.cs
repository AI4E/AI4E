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
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

// TODO: Disposal
// TODO: Rename

namespace AI4E.Coordination
{
    public sealed partial class CoordinationManager<TAddress>
    {
        private sealed class ExchangeManager : ICoordinationExchangeManager<TAddress>
        {
            #region Fields

            private readonly ICoordinationManager _coordinationManager;
            private readonly ISessionManager _sessionManager;
            private readonly IProvider<ICoordinationWaitManager> _waitManager;
            private readonly IProvider<ICoordinationCacheManager> _cacheManager;
            private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
            private readonly IAddressConversion<TAddress> _addressConversion;
            private readonly ILogger _logger;

            private readonly IAsyncProcess _receiveProcess;
            private readonly AsyncInitializationHelper<IPhysicalEndPoint<TAddress>> _initializationHelper;

            #endregion

            #region C'tor

            public ExchangeManager(ICoordinationManager coordinationManager,
                                   ISessionManager sessionManager,
                                   IProvider<ICoordinationWaitManager> waitManager, // TODO: This breaks circular dependency. Can this be implemented better?
                                   IProvider<ICoordinationCacheManager> cacheManager, // TODO: This breaks circular dependency. Can this be implemented better?
                                   IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                                   IAddressConversion<TAddress> addressConversion,
                                   ILogger logger = null)
            {
                if (coordinationManager == null)
                    throw new ArgumentNullException(nameof(coordinationManager));

                if (sessionManager == null)
                    throw new ArgumentNullException(nameof(sessionManager));

                if (waitManager == null)
                    throw new ArgumentNullException(nameof(waitManager));

                if (cacheManager == null)
                    throw new ArgumentNullException(nameof(cacheManager));

                if (endPointMultiplexer == null)
                    throw new ArgumentNullException(nameof(endPointMultiplexer));

                if (addressConversion == null)
                    throw new ArgumentNullException(nameof(addressConversion));

                _coordinationManager = coordinationManager;
                _sessionManager = sessionManager;
                _waitManager = waitManager;
                _cacheManager = cacheManager;
                _endPointMultiplexer = endPointMultiplexer;
                _addressConversion = addressConversion;
                _logger = logger;

                _receiveProcess = new AsyncProcess(ReceiveProcess);
                _initializationHelper = new AsyncInitializationHelper<IPhysicalEndPoint<TAddress>>(InitializeInternalAsync);
            }

            #endregion

            private ICoordinationWaitManager WaitManager => _waitManager.ProvideInstance();
            private ICoordinationCacheManager CacheManager => _cacheManager.ProvideInstance();

            #region Init

            private async Task<IPhysicalEndPoint<TAddress>> InitializeInternalAsync(CancellationToken cancellation)
            {
                var session = await _coordinationManager.GetSessionAsync(cancellation);
                var physicalEndPoint = GetSessionEndPoint(session);

                try
                {
                    await _receiveProcess.StartAsync(cancellation);
                }
                catch
                {
                    await physicalEndPoint.DisposeIfDisposableAsync().HandleExceptionsAsync(_logger);

                    throw;
                }

                _logger?.LogInformation($"[{session}] Initialized {typeof(ExchangeManager).ToString()}.");

                return physicalEndPoint;
            }

            #endregion

            #region ICoordinationExchangeManager

            public ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation)
            {
                return new ValueTask<IPhysicalEndPoint<TAddress>>(_initializationHelper.Initialization.WithCancellation(cancellation));
            }

            public async Task NotifyReadLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation)
            {
                var sessions = await _sessionManager.GetSessionsAsync(cancellation);
                var localSession = await _coordinationManager.GetSessionAsync(cancellation);

                foreach (var session in sessions)
                {
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

            public async Task NotifyWriteLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation)
            {
                var sessions = await _sessionManager.GetSessionsAsync(cancellation);
                var localSession = await _coordinationManager.GetSessionAsync(cancellation);

                foreach (var session in sessions)
                {
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

            public async Task InvalidateCacheEntryAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation)
            {
                if (session == await _coordinationManager.GetSessionAsync(cancellation))
                {
                    await CacheManager.InvalidateCacheEntryAsync(path, cancellation);
                }
                else
                {
                    // The session is the read-lock owner (It caches the entry currently)
                    var message = EncodeMessage(MessageType.InvalidateCacheEntry, path, session);

                    await SendMessageAsync(session, message, cancellation);
                }
            }

            // 0 = false, 1 = true
            private volatile int _isDisposed = 0;

            public void Dispose()
            {
                // Volatile read-op.
                if (_isDisposed != 0)
                    return;

                if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
                {
                    _initializationHelper.Cancel();

                    // Begin receive process termination but do not wait for it to complete.
                    _receiveProcess.Terminate();


                }
            }

            #endregion

            private async Task ReceiveProcess(CancellationToken cancellation)
            {
                var physicalEndPoint = await GetPhysicalEndPointAsync(cancellation);

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        var message = await physicalEndPoint.ReceiveAsync(cancellation);
                        var (messageType, path, session) = DecodeMessage(message);

                        Task.Run(() => HandleMessageAsync(message, messageType, path, session, cancellation)).HandleExceptions();
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception exc)
                    {
                        _logger?.LogWarning(exc, $"[{await _coordinationManager.GetSessionAsync(cancellation)}] Failure while decoding received message.");
                    }
                }
            }

            private async Task HandleMessageAsync(IMessage message, MessageType messageType, CoordinationEntryPath path, Session session, CancellationToken cancellation)
            {
                switch (messageType)
                {
                    case MessageType.InvalidateCacheEntry:
                        if (session != await _coordinationManager.GetSessionAsync(cancellation))
                        {
                            _logger?.LogWarning($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Received invalidate message for session that is not present.");
                        }
                        else
                        {
                            await CacheManager.InvalidateCacheEntryAsync(path, cancellation);
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
                        _logger?.LogWarning($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Received invalid message or message with unknown message type.");
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

                var result = _endPointMultiplexer.GetPhysicalEndPoint(multiplexName);

                Assert(result != null);

                return result;
            }

            private static string GetMultiplexEndPointName(Session session)
            {
                return "coord/" + session.ToString(); // TODO: This should be configurable.
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
}
