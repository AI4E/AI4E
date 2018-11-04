using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Processing;

namespace AI4E.Routing.SignalR.Server
{
    public sealed class ConnectedClientLookup : IConnectedClientLookup, IDisposable
    {
        private static readonly CoordinationEntryPath _basePath = new CoordinationEntryPath("connectedClients"); // TODO: This should be configurable.
        private static readonly TimeSpan _leaseLength = TimeSpan.FromMinutes(10); // TODO: This should be configurable.

        private readonly ICoordinationManager _coordinationManager;
        private readonly IDateTimeProvider _dateTimeProvider;

        private readonly IAsyncProcess _garbageCollectionProcess;

        public ConnectedClientLookup(ICoordinationManager coordinationManager, IDateTimeProvider dateTimeProvider)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _coordinationManager = coordinationManager;
            _dateTimeProvider = dateTimeProvider;
            _garbageCollectionProcess = new AsyncProcess(GarbageCollection, start: true);
        }

        public async Task<(EndPointAddress endPoint, string securityToken)> AddClientAsync(CancellationToken cancellation)
        {
            var securityToken = Guid.NewGuid().ToString();
            var leaseEnd = _dateTimeProvider.GetCurrentTime() + _leaseLength;

            var securityTokenBytesCount = Encoding.UTF8.GetByteCount(securityToken);

            using (ArrayPool<byte>.Shared.Rent(8 + 4 + securityTokenBytesCount, out var bytes))
            {
                var payloadLength = EncodePayload(bytes, securityToken.AsSpan(), leaseEnd);
                var payload = bytes.AsMemory().Slice(start: 0, payloadLength);

                EndPointAddress endPoint;
                do
                {
                    endPoint = new EndPointAddress("client/" + Guid.NewGuid().ToString());

                    try
                    {
                        await _coordinationManager.CreateAsync(_basePath.GetChildPath(endPoint.ToString()), payload, cancellation: cancellation);

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

        public async Task<bool> ValidateClientAsync(EndPointAddress endPoint, string securityToken, CancellationToken cancellation)
        {
            var path = _basePath.GetChildPath(endPoint.ToString());

            do
            {
                var entry = await _coordinationManager.GetAsync(path, cancellation: cancellation);

                if (entry == null)
                {
                    return false;
                }

                var (comparandSecurityToken, leaseEnd) = DecodePayload(entry.Value.Span);
                var now = _dateTimeProvider.GetCurrentTime();

                // We have to assume that the client is not connected anymore.
                // This is a race condition, that has to be prevented.
                if (now < leaseEnd)
                {
                    await _coordinationManager.DeleteAsync(entry.Path, cancellation: cancellation);

                    var clientsDisconnected = Volatile.Read(ref ClientsDisconnected);

                    if (clientsDisconnected != null && clientsDisconnected.GetInvocationList().Any())
                    {
                        var clientEndPoints = new EndPointAddress[] { new EndPointAddress(entry.Name.Segment.Span) };
                        var eventArgs = new ClientsDisconnectedEventArgs(clientEndPoints);

                        clientsDisconnected(this, eventArgs);
                    }

                    return false;
                }

                if (securityToken != comparandSecurityToken)
                {
                    return false;
                }

                var newLeaseEnd = now + _leaseLength;

                if (newLeaseEnd > leaseEnd)
                {
                    leaseEnd = newLeaseEnd;
                }

                var securityTokenBytesCount = Encoding.UTF8.GetByteCount(securityToken);

                using (ArrayPool<byte>.Shared.Rent(8 + 4 + securityTokenBytesCount, out var bytes))
                {
                    var payloadLength = EncodePayload(bytes, securityToken.AsSpan(), leaseEnd);
                    var payload = bytes.AsMemory().Slice(start: 0, payloadLength);
                    endPoint = new EndPointAddress("client/" + Guid.NewGuid().ToString());

                    var version = await _coordinationManager.SetValueAsync(path, payload, version: entry.Version, cancellation: cancellation);

                    if (version == entry.Version)
                    {
                        return true;
                    }
                }
            }
            while (true);
        }

        public event EventHandler<ClientsDisconnectedEventArgs> ClientsDisconnected;

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

        private async Task GarbageCollection(CancellationToken cancellation)
        {
            ICollection<IEntry> disconnectedClients = new LinkedList<IEntry>();

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var delay = TimeSpan.FromSeconds(2); // TODO: This should be configurable.
                    disconnectedClients.Clear();

                    var rootNode = await _coordinationManager.GetAsync(_basePath, cancellation);
                    var clients = rootNode.GetChildrenEntries();

                    var enumerator = clients.GetEnumerator();
                    try
                    {
                        while (await enumerator.MoveNext(cancellation))
                        {
                            var client = enumerator.Current;
                            var (_, leaseEnd) = DecodePayload(client.Value.Span);

                            var now = _dateTimeProvider.GetCurrentTime();
                            if (now < leaseEnd)
                            {
                                disconnectedClients.Add(client);
                                continue;
                            }

                            var timeToWait = leaseEnd - now;
                            if (timeToWait < delay)
                                delay = timeToWait;
                        }
                    }
                    finally
                    {
                        enumerator.Dispose();
                    }

                    await Task.WhenAll(disconnectedClients.Select(p => _coordinationManager.DeleteAsync(p.Path, cancellation: cancellation).AsTask()));

                    var clientsDisconnected = Volatile.Read(ref ClientsDisconnected);

                    if (clientsDisconnected != null && clientsDisconnected.GetInvocationList().Any())
                    {
                        var clientEndPoints = disconnectedClients.Select(p => new EndPointAddress(p.Name.Segment.Span)).ToList();
                        var eventArgs = new ClientsDisconnectedEventArgs(clientEndPoints);

                        clientsDisconnected(this, eventArgs);
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

        public void Dispose()
        {
            _garbageCollectionProcess.Terminate();
        }
    }
}
