using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class CoordinationCallback<TAddress> : ICoordinationCallback, ISessionProvider, IAsyncDisposable
    {
        private readonly IPhysicalEndPoint<TAddress> _physicalEndPoint;
        private readonly IProvider<ICoordinationManager> _coordinationManagerProvider;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private readonly ILogger<CoordinationCallback<TAddress>> _logger;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly IAsyncProcess _receiveProcess;

        public CoordinationCallback(IPhysicalEndPoint<TAddress> physicalEndPoint,
                                    IProvider<ICoordinationManager> coordinationManagerProvider,
                                    IAddressConversion<TAddress> addressConversion,
                                    ILogger<CoordinationCallback<TAddress>> logger)
        {
            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            if (coordinationManagerProvider == null)
                throw new ArgumentNullException(nameof(coordinationManagerProvider));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _physicalEndPoint = physicalEndPoint;
            _coordinationManagerProvider = coordinationManagerProvider;
            _addressConversion = addressConversion;
            _logger = logger;

            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _receiveProcess = new AsyncProcess(ReceiveProcess);
        }

        public string GetSession()
        {
            return GetNextSessionFromAddress(LocalAddress);
        }

        public async Task InvalidateCacheEntryAsync(string entry, string session, CancellationToken cancellation)
        {
            var coordinationManager = _coordinationManagerProvider.ProvideInstance();

            Assert(coordinationManager != null);

            if (session == await coordinationManager.GetSessionAsync(cancellation))
            {
                await InvalidateLocalCacheEntryAsync(entry, session, cancellation);
            }
            else
            {
                var remoteAddress = GetAddressFromSession(session);

                Assert(remoteAddress != null);

                var message = new Message();

                EncodeMessage(message, MessageType.InvalidateCacheEntry, entry, session);

                await _physicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
        }

        private void EncodeMessage(IMessage message, MessageType messageType, string entry, string session)
        {
            Assert(message != null);
            // Modify if other message types are added
            Assert(messageType >= MessageType.InvalidateCacheEntry && messageType <= MessageType.InvalidateCacheEntry);

            using (var frameStream = message.PushFrame().OpenStream())
            using (var binaryWriter = new BinaryWriter(frameStream))
            {
                binaryWriter.Write((byte)messageType);

                var entryBytes = Encoding.UTF8.GetBytes(entry);
                binaryWriter.Write(entryBytes.Length);
                binaryWriter.Write(entryBytes);

                var sessionBytes = Encoding.UTF8.GetBytes(session);
                binaryWriter.Write(sessionBytes.Length);
                binaryWriter.Write(sessionBytes);
            }
        }

        private async Task InvalidateLocalCacheEntryAsync(string entry, string session, CancellationToken cancellation)
        {
            var coordinationManager = _coordinationManagerProvider.ProvideInstance();

            Assert(coordinationManager != null);

            if (session != await coordinationManager.GetSessionAsync(cancellation))
            {
                _logger?.LogWarning("Received invalidate message for session that is not present.");
            }
            else
            {
                await coordinationManager.InvalidateCacheEntryAsync(entry, cancellation);
            }
        }

        public TAddress LocalAddress => _physicalEndPoint.LocalAddress;

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = await ReceiveMessageAsync(cancellation);
                    var (messageType, entry, session) = DecodeMessage(message);

                    HandleMessageAsync(message, messageType, entry, session, cancellation).HandleExceptions();

                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Failure while decoding received message.");
                }
            }
        }

        private Task HandleMessageAsync(IMessage message, MessageType messageType, string entry, string session, CancellationToken cancellation)
        {
            switch (messageType)
            {
                case MessageType.InvalidateCacheEntry:
                    return InvalidateLocalCacheEntryAsync(entry, session, cancellation);

                case MessageType.Unknown:
                default:
                    _logger?.LogWarning("Received invalid message or message with unknown message type.");
                    break;
            }

            return Task.CompletedTask;
        }

        private Task<IMessage> ReceiveMessageAsync(CancellationToken cancellation)
        {
            return _physicalEndPoint.ReceiveAsync(cancellation);
        }

        private (MessageType messageType, string entry, string session) DecodeMessage(IMessage message)
        {
            Assert(message != null);

            var messageType = default(MessageType);
            var session = default(string);
            var entry = default(string);

            using (var frameStream = message.PopFrame().OpenStream())
            using (var binaryReader = new BinaryReader(frameStream))
            {
                messageType = (MessageType)binaryReader.ReadByte();

                var entryLength = binaryReader.ReadInt32();
                var entryBytes = binaryReader.ReadBytes(entryLength);
                entry = Encoding.UTF8.GetString(entryBytes);
                entry = Encoding.UTF8.GetString(entryBytes);

                var sessionLength = binaryReader.ReadInt32();
                var sessionBytes = binaryReader.ReadBytes(sessionLength);
                session = Encoding.UTF8.GetString(sessionBytes);
            }

            return (messageType, entry, session);
        }

        #endregion

        #region Initialization

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
                await _initializationHelper.CancelAsync();
            }
            finally
            {
                await _receiveProcess.TerminateAsync();
            }
        }

        #endregion

        private static int _counter = 0;

        // Creates a new unique session identifier for the specified address.
        private string GetNextSessionFromAddress(TAddress address)
        {
            // The session is mainly the local physical address 
            // combined with a prefix to distinguish between session 
            // with the same physical address that live one after another.

            // THe prefix is the current timestamp with a disciriminator 
            // added to distinguish between sessions created at the same time.
            var count = Interlocked.Increment(ref _counter);
            var ticks = DateTime.Now.Ticks + count;

            var prefix = BitConverter.GetBytes(ticks);
            var serializedAddress = _addressConversion.SerializeAddress(address);

            var arr = new byte[prefix.Length + serializedAddress.Length];

            Array.Copy(prefix, arr, prefix.Length);
            Array.Copy(serializedAddress, 0, arr, prefix.Length, serializedAddress.Length);

            return Convert.ToBase64String(arr);
        }

        private TAddress GetAddressFromSession(string session)
        {
            var arr = Convert.FromBase64String(session);

            var serializedAddress = new byte[arr.Length - 8];

            Array.Copy(arr, 0, serializedAddress, 8, serializedAddress.Length);

            return _addressConversion.DeserializeAddress(serializedAddress);
        }

        private enum MessageType : byte
        {
            Unknown = 0,
            InvalidateCacheEntry = 1
        }
    }
}
