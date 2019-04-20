using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Utils;
using AI4E.Utils.Memory;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    // TODO: Rename
    public sealed class ClientConnectionManager : IClientConnectionManager, IDisposable
    {
        #region Fields

        private readonly ICoordinationManager _coordinationManager;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<ClientConnectionManager> _logger;

        private readonly ClientConnectionOptions _clientConnectionOptions;
        private readonly ClientConnectionManagerOptions _clientConnectionManagerOptions;

        private readonly IAsyncProcess _garbageCollectionProcess;
        private readonly Dictionary<EndPointAddress, Task> _clientDisconnectCache = new Dictionary<EndPointAddress, Task>();
        private readonly object _lock = new object();
        private volatile CancellationTokenSource _disposalSource = new CancellationTokenSource();

        #endregion

        #region C'tor

        public ClientConnectionManager(ICoordinationManager coordinationManager,
                                       IDateTimeProvider dateTimeProvider,
                                       IOptions<ClientConnectionOptions> clientConnectionOptionsAccessor,
                                       IOptions<ClientConnectionManagerOptions> clientConnectionManagerOptionsAccessor,
                                       ILogger<ClientConnectionManager> logger = null)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (clientConnectionOptionsAccessor == null)
                throw new ArgumentNullException(nameof(clientConnectionOptionsAccessor));

            if (clientConnectionManagerOptionsAccessor == null)
                throw new ArgumentNullException(nameof(clientConnectionManagerOptionsAccessor));

            _coordinationManager = coordinationManager;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;

            _clientConnectionOptions = clientConnectionOptionsAccessor.Value ?? new ClientConnectionOptions();
            _clientConnectionManagerOptions = clientConnectionManagerOptionsAccessor.Value ?? new ClientConnectionManagerOptions();

            Timeout = _clientConnectionOptions.Timeout <= TimeSpan.Zero ? ClientConnectionOptions.DefaultTimeout : _clientConnectionOptions.Timeout;
            BasePath = new CoordinationEntryPath(string.IsNullOrEmpty(_clientConnectionManagerOptions.BasePath) ? ClientConnectionManagerOptions.DefaultBasePath : _clientConnectionManagerOptions.BasePath);
            GarbageCollectionDelayMax = _clientConnectionManagerOptions.GarbageCollectionDelayMax <= TimeSpan.Zero ? ClientConnectionManagerOptions.DefaultGarbageCollectionDelayMax : _clientConnectionManagerOptions.GarbageCollectionDelayMax;


            _garbageCollectionProcess = new AsyncProcess(GarbageCollection, start: true);
        }

        #endregion

        public TimeSpan Timeout { get; }

        private CoordinationEntryPath BasePath { get; }

        private TimeSpan GarbageCollectionDelayMax { get; }

        private EndPointAddress AllocateEndPointAddress()
        {
            var prefix = _clientConnectionManagerOptions.EndPointPrefix;

            var prefixLength = string.IsNullOrEmpty(prefix) ? 0 : (Encoding.UTF8.GetByteCount(prefix) + 1);
            var guid = Guid.NewGuid();

            // TODO: Optimize this.
            var encodedGuidBytes = Encoding.UTF8.GetBytes(Convert.ToBase64String(guid.ToByteArray()).Substring(0, 22).Replace("/", "_").Replace("+", "-"));
            Assert(encodedGuidBytes.Length == 22);

            var addressBytes = new byte[prefixLength + 22];

            if (prefixLength > 0)
            {
                var bytesWritten = Encoding.UTF8.GetBytes(prefix.AsSpan(), addressBytes.AsSpan());

                Assert(bytesWritten == prefixLength - 1);

                addressBytes[bytesWritten] = 0x2F;
            }

            encodedGuidBytes.CopyTo(addressBytes.AsSpan().Slice(addressBytes.Length - 22));

            return new EndPointAddress(addressBytes);
        }

        #region IConnectedClientLookup

        public async Task<(EndPointAddress endPoint, string securityToken)> AddClientAsync(CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    return await AddClientCoreAsync(cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task<bool> ValidateClientAsync(EndPointAddress endPoint, string securityToken, CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    return await ValidateClientCoreAsync(endPoint, securityToken, cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        //public async Task<bool> DisconnectAsync(EndPointAddress endPoint, string securityToken, CancellationToken cancellation)

        // TODO: WaitForDisconnectAsync must return a completed task, if the client is not connected at the time of call.
        public async Task WaitForDisconnectAsync(EndPointAddress endPoint, CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var disposal))
            {
                try
                {
                    async Task BuildClientDisconnectCacheEntry()
                    {
                        try
                        {
                            // Yields back the task, that represents the asnyc state machine immediately to leave the critical section as fast as possible.
                            await Task.Yield();
                            await WaitForDisconnectCoreAsync(endPoint);
                        }
                        finally
                        {
                            lock (_lock)
                            {
                                _clientDisconnectCache.Remove(endPoint);
                            }
                        }
                    }

                    Task task;

                    lock (_lock)
                    {
                        if (!_clientDisconnectCache.TryGetValue(endPoint, out task))
                        {
                            task = BuildClientDisconnectCacheEntry();
                            _clientDisconnectCache.Add(endPoint, task);
                        }
                    }

                    await task.WithCancellation(cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        #endregion

        private async Task<(EndPointAddress endPoint, string securityToken)> AddClientCoreAsync(CancellationToken cancellation)
        {
            var now = _dateTimeProvider.GetCurrentTime();
            var securityToken = Guid.NewGuid().ToString();
            var leaseEnd = now + Timeout;
            var securityTokenBytesCount = Encoding.UTF8.GetByteCount(securityToken);

            using (ArrayPool<byte>.Shared.Rent(8 + 4 + securityTokenBytesCount, out var bytes))
            {
                var payloadLength = EncodePayload(bytes, securityToken.AsSpan(), leaseEnd);
                var payload = bytes.AsMemory().Slice(start: 0, payloadLength);

                EndPointAddress endPoint;
                do
                {
                    endPoint = AllocateEndPointAddress();
                    var path = GetPath(endPoint);

                    try
                    {
                        await _coordinationManager.CreateAsync(path, payload, cancellation: cancellation);

                        _logger?.LogDebug($"Assigning end-point '{endPoint.ToString()}' to recently connected client.");

                        break;
                    }
                    catch (DuplicateEntryException) // TODO: Add a TryCreateAsync method to the coordination service.
                    {
                        continue;
                    }
                }
                while (cancellation.ThrowOrContinue());

                return (endPoint, securityToken);
            }
        }

        private async Task<bool> ValidateClientCoreAsync(EndPointAddress endPoint,
                                                         string securityToken,
                                                         CancellationToken cancellation)
        {
            var path = GetPath(endPoint);
            do
            {
                var now = _dateTimeProvider.GetCurrentTime();
                var entry = await _coordinationManager.GetAsync(path, cancellation: cancellation);

                if (entry == null)
                {
                    _logger?.LogDebug($"Cannot update session for client with end-point '{endPoint.ToString()}'. Session is terminated.");

                    return false;
                }

                var (comparandSecurityToken, leaseEnd) = DecodePayload(entry.Value.Span);

                // We have to assume that the client is not connected anymore.
                // This is a race condition, that has to be prevented.
                if (now >= leaseEnd)
                {
                    await _coordinationManager.DeleteAsync(entry.Path, cancellation: cancellation);

                    _logger?.LogDebug($"Session for client with end-point '{endPoint.ToString()}' is terminated. Removing entry.");
                    _logger?.LogDebug($"Cannot update session for client with end-point '{endPoint.ToString()}'. Session is terminated.");
                    return false;
                }

                if (securityToken != comparandSecurityToken)
                {
                    _logger?.LogDebug($"Cannot update session for client with end-point '{endPoint.ToString()}'. Session is terminated.");
                    return false;
                }

                var newLeaseEnd = now + Timeout;

                if (newLeaseEnd > leaseEnd)
                {
                    leaseEnd = newLeaseEnd;
                }

                var securityTokenBytesCount = Encoding.UTF8.GetByteCount(securityToken);

                using (ArrayPool<byte>.Shared.Rent(8 + 4 + securityTokenBytesCount, out var bytes))
                {
                    var payloadLength = EncodePayload(bytes, securityToken.AsSpan(), leaseEnd);
                    var payload = bytes.AsMemory().Slice(start: 0, payloadLength);
                    var version = await _coordinationManager.SetValueAsync(path, payload, version: entry.Version, cancellation: cancellation);

                    if (version == entry.Version)
                    {
                        _logger?.LogDebug($"Updated session for client with end-point '{endPoint.ToString()}'.");

                        return true;
                    }
                }
            }
            while (true);
        }

        // This does not receive a cancellation token, as the result of this is cached and must not depend on a callers cancellation token.
        private async Task WaitForDisconnectCoreAsync(EndPointAddress endPoint)
        {
            var disposalSource = _disposalSource; // Volatile read op.

            if (disposalSource.IsCancellationRequested)
                throw new ObjectDisposedException(GetType().FullName);

            var disposal = disposalSource.Token;

            try
            {
                var path = GetPath(endPoint);
                var entry = await _coordinationManager.GetAsync(path, disposal);

                while (entry != null)
                {
                    var now = _dateTimeProvider.GetCurrentTime();
                    var (_, leaseEnd) = DecodePayload(entry.Value.Span);

                    if (now >= leaseEnd)
                    {
                        return;
                    }

                    var timeToWait = leaseEnd - now;
                    await Task.Delay(timeToWait, disposal);

                    entry = await _coordinationManager.GetAsync(path, disposal);
                }
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private async Task GarbageCollection(CancellationToken cancellation)
        {
            ICollection<IEntry> disconnectedClients = new LinkedList<IEntry>();

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var delay = GarbageCollectionDelayMax;
                    disconnectedClients.Clear();

                    var rootNode = await _coordinationManager.GetAsync(BasePath, cancellation);

                    if (rootNode != null)
                    {
                        var clients = rootNode.GetChildrenEntries();

#if SUPPORTS_ASYNC_ENUMERABLE
                        await foreach(var client in clients)
                        {
#else
                        var enumerator = clients.GetEnumerator();
                        try
                        {
                            while (await enumerator.MoveNext(cancellation))
                            {
                                var client = enumerator.Current;
#endif
                                var (securityToken, leaseEnd) = DecodePayload(client.Value.Span);

                                var now = _dateTimeProvider.GetCurrentTime();
                                if (now >= leaseEnd)
                                {
                                    if (_logger.IsEnabled(LogLevel.Debug))
                                    {
                                        _logger?.LogDebug($"Session for client with end-point '{client.Name.ToString()}' is terminated. Removing entry.");
                                    }

                                    disconnectedClients.Add(client);
                                    continue;
                                }

                                var timeToWait = leaseEnd - now;
                                if (timeToWait < delay)
                                    delay = timeToWait;
#if !SUPPORTS_ASYNC_ENUMERABLE
                            }
                        }
                        finally
                        {
                            enumerator.Dispose();
#endif
                        }

                        await Task.WhenAll(disconnectedClients.Select(p => _coordinationManager.DeleteAsync(p.Path, cancellation: cancellation).AsTask()));
                    }

                    await Task.Delay(delay, cancellation);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    // TODO: Log
                }
            }
        }

        private CoordinationEntryPath GetPath(EndPointAddress endPoint)
        {
            return BasePath.GetChildPath(endPoint.ToString());
        }

        #region Coding

        private int EncodePayload(Span<byte> span, ReadOnlySpan<char> securityToken, DateTime leaseEnd)
        {
            var writer = new BinarySpanWriter(span);
            writer.Write(securityToken, lengthPrefix: true);
            writer.WriteInt64(leaseEnd.Ticks);

            return writer.Length;
        }

        private (string securityToken, DateTime leaseEnd) DecodePayload(ReadOnlySpan<byte> span)
        {
            var reader = new BinarySpanReader(span);
            var securityToken = reader.ReadString();
            var leaseEnd = new DateTime(reader.ReadInt64());
            return (securityToken, leaseEnd);
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource != null)
            {
                disposalSource.Cancel();
                _garbageCollectionProcess.Terminate();
                disposalSource.Dispose();
            }
        }

        private IDisposable CheckDisposal(ref CancellationToken cancellation,
                                          out CancellationToken disposal)
        {
            var disposalSource = _disposalSource; // Volatile read op

            if (disposalSource == null)
                throw new ObjectDisposedException(GetType().FullName);

            disposal = disposalSource.Token;

            if (cancellation.CanBeCanceled)
            {
                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, disposal);
                cancellation = combinedCancellationSource.Token;

                return combinedCancellationSource;
            }
            else
            {
                cancellation = disposal;

                return NoOpDisposable.Instance;
            }
        }

        #endregion
    }
}
