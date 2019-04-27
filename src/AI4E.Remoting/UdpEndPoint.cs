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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Remoting
{
    public sealed class UdpEndPoint : IPhysicalEndPoint<IPEndPoint>
    {
#pragma warning disable IDE1006
        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms740668(v=vs.85).aspx
        private const int WSAECONNRESET = 10054;
#pragma warning restore IDE1006

        private const int UdpPayloadLimit = 548;
        private const int UdpPayloadLimitWithoutHeader = UdpPayloadLimit - 12;
        private const int UdpPayloadLimitWithoutHeaderMinusOne = UdpPayloadLimitWithoutHeader - 1;

        private static readonly TimeSpan _reveiveTimeout = TimeSpan.FromSeconds(30); // TODO: This should be configurable
        private static readonly TimeSpan _gcTimeout = TimeSpan.FromSeconds(60); // TODO: This should be configurable

        private readonly ILogger<UdpEndPoint> _logger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly UdpClient _udpClient;

        private readonly AsyncProducerConsumerQueue<Transmission<IPEndPoint>> _rxQueue;
        private readonly AsyncManualResetEvent _event = new AsyncManualResetEvent(set: false);

        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncProcess _blockGCProcess;

        private bool _isDisposed = false;
        private int _seqNum;

        private readonly Dictionary<(IPEndPoint remoteEndPoint, int seqNum), LinkedListNode<BlockSequence>> _blockCache
           = new Dictionary<(IPEndPoint remoteEndPoint, int seqNum), LinkedListNode<BlockSequence>>();
        private readonly LinkedList<BlockSequence> _blocksByLastWriteTime = new LinkedList<BlockSequence>();
        private readonly object _mutex = new object();

        public UdpEndPoint(IDateTimeProvider dateTimeProvider, ILogger<UdpEndPoint> logger)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
            _rxQueue = new AsyncProducerConsumerQueue<Transmission<IPEndPoint>>();

            var localAddress = GetLocalAddress();

            if (localAddress == null)
            {
                throw new Exception("Cannot evaluate local address."); // TODO: https://github.com/AI4E/AI4E/issues/32
            }

            // We generate an IPv4 end-point for now.
            // TODO: https://github.com/AI4E/AI4E/issues/30
            _udpClient = new UdpClient(new IPEndPoint(localAddress, port: 0));

            LocalAddress = new IPEndPoint(localAddress, ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO: Does this work in linux/unix too?
                //       https://github.com/AI4E/AI4E/issues/29
                // See: https://stackoverflow.com/questions/7201862/an-existing-connection-was-forcibly-closed-by-the-remote-host
                uint IOC_IN = 0x80000000,
                     IOC_VENDOR = 0x18000000,
                     SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                _udpClient.Client.IOControl(unchecked((int)SIO_UDP_CONNRESET), new byte[] { Convert.ToByte(false) }, null);
            }

            _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
            _blockGCProcess = new AsyncProcess(BlockGCProcess, start: true);
        }

        #region AddressConversion

        /// <inheritdoc />
        public string AddressToString(IPEndPoint address)
        {
            return IPEndPointConverter.AddressToString(address);
        }

        /// <inheritdoc />
        public IPEndPoint AddressFromString(string str)
        {
            return IPEndPointConverter.AddressFromString(str);
        }

        #endregion

        public async Task SendAsync(Transmission<IPEndPoint> transmission, CancellationToken cancellation)
        {
            if (transmission.Equals(default)) // TODO: Use ==
                throw new ArgumentDefaultException(nameof(transmission));

            if (transmission.RemoteAddress.Equals(LocalAddress))
            {
                await _rxQueue.EnqueueAsync(new Transmission<IPEndPoint>(transmission.Message, LocalAddress), cancellation);
                _event.Set();
                return;
            }

            var blocks = await Block.GetBlocksAsync(transmission.Message, cancellation);
            var seqNum = Interlocked.Increment(ref _seqNum);

            for (var i = 0; i < blocks.Count; i++)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(blocks[i].Payload.Length + 12);

                try
                {
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(), seqNum);
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan().Slice(4), blocks.Count);
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan().Slice(8), i);

                    blocks[i].Payload.CopyTo(buffer.AsMemory().Slice(12));

                    Console.WriteLine($"UDP EndPoint: Transmit block #{ i } of { blocks.Count} to {transmission.RemoteAddress.ToString()} with seq-num {seqNum}.");

                    await _udpClient.SendAsync(buffer, blocks[i].Payload.Length + 12, transmission.RemoteAddress).WithCancellation(cancellation);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public Task<Transmission<IPEndPoint>> ReceiveAsync(CancellationToken cancellation)
        {
            return _rxQueue.DequeueAsync(cancellation);
        }

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Physical-end-point {LocalAddress}: Receive process started.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    UdpReceiveResult receiveResult;
                    try
                    {
                        receiveResult = await _udpClient.ReceiveAsync().WithCancellation(cancellation);
                    }
                    // Apparently, the udp socket does receive ICMP messages that a remote host was unreachable on sending
                    // and throws an exception on the next receive call. We just ignore this currently.
                    // https://msdn.microsoft.com/en-us/library/windows/desktop/ms740120%28v=vs.85%29.aspx
                    catch (SocketException exc) when (exc.ErrorCode == WSAECONNRESET)
                    {
                        continue;
                    }
                    catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { return; }

                    var payload = receiveResult.Buffer;
                    var seqNum = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan());
                    var blockCount = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan().Slice(4));

                    if (blockCount == 1)
                    {
                        await PutToRxQueueAsync(receiveResult.RemoteEndPoint, payload, index: 12, payload.Length - 12, cancellation);
                    }
                    else
                    {
                        var block = new Block(payload.AsMemory().Slice(8));

                        Console.WriteLine($"UDP EndPoint: Received block #{ block.Number } of { blockCount} from {receiveResult.RemoteEndPoint.ToString()} with seq-num {seqNum}.");

                        BlockSequence sequence;
                        var isComplete = false;

                        lock (_mutex)
                        {
                            if (!_blockCache.TryGetValue((receiveResult.RemoteEndPoint, seqNum), out var sequenceNode))
                            {
                                sequence = new BlockSequence(receiveResult.RemoteEndPoint, seqNum, blockCount);
                                sequenceNode = new LinkedListNode<BlockSequence>(sequence);
                                _blockCache.Add((receiveResult.RemoteEndPoint, seqNum), sequenceNode);
                            }
                            else
                            {
                                sequence = sequenceNode.Value;
                            }

                            if (sequenceNode.List == _blocksByLastWriteTime)
                            {
                                _blocksByLastWriteTime.Remove(sequenceNode);
                            }

                            if (sequence.SetBlock(block))
                            {
                                _blockCache.Remove((receiveResult.RemoteEndPoint, seqNum));
                                isComplete = true;
                            }
                            else
                            {
                                _blocksByLastWriteTime.AddLast(sequenceNode);
                            }
                        }

                        if (isComplete)
                        {
                            Console.WriteLine($"UDP EndPoint: Block sequence from {receiveResult.RemoteEndPoint.ToString()} with seq-num {seqNum} complete.");

                            var bufferLength = sequence.Sum(p => p.Payload.Length);
                            var buffer = ArrayPool<byte>.Shared.Rent(bufferLength);

                            try
                            {
                                var bufferIndex = 0;
                                foreach (var b in sequence)
                                {
                                    b.Payload.CopyTo(buffer.AsMemory().Slice(bufferIndex));
                                    bufferIndex += b.Payload.Length;
                                }

                                await PutToRxQueueAsync(receiveResult.RemoteEndPoint, buffer, 0, bufferLength, cancellation);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }

                // TODO: https://github.com/AI4E/AI4E/issues/33
                //       This can end in an infinite loop, f.e. if the socket is down.
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Physical-end-point {LocalAddress}: Failure on receiving incoming message.");
                }
            }
        }

        private async Task BlockGCProcess(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Physical-end-point {LocalAddress}: GC process started.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var now = _dateTimeProvider.GetCurrentTime();

                    lock (_mutex)
                    {
                        for (var current = _blocksByLastWriteTime.First;
                             current != null && current.Value.LastWriteTime + _reveiveTimeout < now;
                              current = current.Next)
                        {
                            _blocksByLastWriteTime.Remove(current);
                            _blockCache.Remove((current.Value.RemoteEndPoint, current.Value.SeqNum));
                        }
                    }

                    await Task.Delay(_gcTimeout, cancellation); // TODO
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }

                // TODO: https://github.com/AI4E/AI4E/issues/33
                //       This can end in an infinite loop, f.e. if the socket is down.
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Physical-end-point {LocalAddress}: Failure on receiving incoming message.");
                }
            }
        }

        private async Task PutToRxQueueAsync(IPEndPoint remoteEndPoint, byte[] payload, int index, int length, CancellationToken cancellation)
        {
            var message = new Message();

            using (var memoryStream = new MemoryStream(payload, index, length))
            {
                await message.ReadAsync(memoryStream, cancellation);
            }

            await _rxQueue.EnqueueAsync(new Transmission<IPEndPoint>(message, remoteEndPoint), cancellation);
        }

        public IPEndPoint LocalAddress { get; }
        private IPAddress GetLocalAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                // TODO: https://github.com/AI4E/AI4E/issues/31
                // TODO: https://github.com/AI4E/AI4E/issues/30
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                try
                {
                    try
                    {
                        _receiveProcess.Terminate();
                    }
                    finally
                    {
                        _blockGCProcess.Terminate();
                    }
                }
                finally
                {
                    _udpClient.Close();
                }
            }
        }

        private readonly struct Block
        {
            public Block(int number, ReadOnlyMemory<byte> payload)
            {
                Number = number;
                Payload = payload;
            }

            public Block(ReadOnlyMemory<byte> bytes)
            {
                Number = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span);
                Payload = bytes.Slice(4);
            }

            public int Number { get; }
            public ReadOnlyMemory<byte> Payload { get; }

            public static IReadOnlyList<Block> GetBlocks(ReadOnlyMemory<byte> payload)
            {
                var blockCount = GetBlockCount(payload.Length);
                var result = ImmutableList.CreateBuilder<Block>();

                for (var i = 0; i < blockCount; i++)
                {
                    var payloadIndex = i * UdpPayloadLimitWithoutHeader;
                    var payloadLength = Math.Min(UdpPayloadLimitWithoutHeader, payload.Length - payloadIndex);
                    result.Add(new Block(i, payload.Slice(payloadIndex, payloadLength)));
                }

                return result;
            }

            public static async Task<IReadOnlyList<Block>> GetBlocksAsync(IMessage message, CancellationToken cancellation)
            {
                var buffer = new byte[message.Length];

                using (var memoryStream = new MemoryStream(buffer, writable: true))
                {
                    await message.WriteAsync(memoryStream, cancellation);
                }

                return GetBlocks(buffer);
            }

            private static int GetBlockCount(int messageLength)
            {
                return (messageLength + UdpPayloadLimitWithoutHeaderMinusOne) / UdpPayloadLimitWithoutHeader;
            }
        }

        private sealed class BlockSequence : IEnumerable<Block>
        {
            private readonly (Block block, bool set)[] _blocks;
            private DateTime _lastWriteTime;
            private readonly object _mutex = new object();

            public BlockSequence(IPEndPoint remoteEndPoint, int seqNum, int numberOfBlocks)
            {
                _lastWriteTime = DateTime.UtcNow; // TODO: Use DateTimeProvider
                _blocks = new (Block block, bool set)[numberOfBlocks];
                RemoteEndPoint = remoteEndPoint;
                SeqNum = seqNum;
            }

            public DateTime LastWriteTime
            {
                get
                {
                    lock (_mutex)
                    {
                        return _lastWriteTime;
                    }
                }
            }

            public IPEndPoint RemoteEndPoint { get; }
            public int SeqNum { get; }

            public bool SetBlock(Block block)
            {
                lock (_mutex)
                {
                    _lastWriteTime = DateTime.UtcNow; // TODO: Use DateTimeProvider
                }

                _blocks[block.Number] = (block, true);
                return _blocks.All(p => p.set);
            }

            public IEnumerator<Block> GetEnumerator()
            {
                Debug.Assert(_blocks.All(p => p.set));

                return _blocks.Select(p => p.block).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
