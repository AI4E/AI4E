using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    /// <summary>
    /// A two way lookup for outgoing messages, storing messages until acknoledged.
    /// </summary>
    /// <remarks>
    /// This type is thread-safe.
    /// </remarks>
    public sealed class OutboundMessageLookup
    {
        private readonly object _lock = new object();

        // Stores messages indexed by the message seq-num. (One per seq-num)
        private readonly Dictionary<int, (string address, ReadOnlyMemory<byte> bytes, TaskCompletionSource<object> ackSource)> _bySeqNum;

        // Stored messages indexed by the receiver address. (Multiple per address)
        private readonly Dictionary<string, Dictionary<int, ReadOnlyMemory<byte>>> _byAddress;

        /// <summary>
        /// Creates a new instance of the <see cref="OutboundMessageLookup"/> type.
        /// </summary>
        public OutboundMessageLookup()
        {
            _bySeqNum = new Dictionary<int, (string address, ReadOnlyMemory<byte> bytes, TaskCompletionSource<object> ackSource)>();
            _byAddress = new Dictionary<string, Dictionary<int, ReadOnlyMemory<byte>>>();
        }

        /// <summary>
        /// Tries to add an outgoing message to the lookup.
        /// </summary>
        /// <param name="seqNum">The seq-num of the outging message.</param>
        /// <param name="address">The address of the message receiver.</param>
        /// <param name="bytes">The message payload.</param>
        /// <param name="ackSource">The transmission operations task source</param>
        /// <returns>True if the message was added successfully, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="address"/> or <paramref name="ackSource"/> is null.
        /// </exception>
        public bool TryAdd(int seqNum, string address, ReadOnlyMemory<byte> bytes, TaskCompletionSource<object> ackSource)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (ackSource == null)
                throw new ArgumentNullException(nameof(ackSource));

            lock (_lock)
            {
                if (!_bySeqNum.TryAdd(seqNum, (address, bytes, ackSource)))
                    return false;

                if (!_byAddress.TryGetValue(address, out var messages))
                {
                    messages = new Dictionary<int, ReadOnlyMemory<byte>>();
                    _byAddress.Add(address, messages);
                }

                var success = messages.TryAdd(seqNum, bytes);

                Assert(success);
            }

            return true;
        }

        /// <summary>
        /// Tries to remove the message with the specified seq-num from the lookup.
        /// </summary>
        /// <param name="seqNum">The messages seq-num.</param>
        /// <param name="address">Contains the receiver address, if the removal was successfull.</param>
        /// <param name="bytes">Contains the messages payload, if the removal was successfull.</param>
        /// <param name="ackSource">Contains the transmission operations task source, if the removal was successfull.</param>
        /// <returns>True if the message was removed successfully, false otherwise.</returns>
        public bool TryRemove(int seqNum, out string address, out ReadOnlyMemory<byte> bytes, out TaskCompletionSource<object> ackSource)
        {
            lock (_lock)
            {
                if (!_bySeqNum.Remove(seqNum, out var entry))
                {
                    bytes = default;
                    address = default;
                    ackSource = default;

                    return false;
                }

                bytes = entry.bytes;
                address = entry.address;
                ackSource = entry.ackSource;

                var success = _byAddress.TryGetValue(address, out var messages) &&
                              messages.Remove(seqNum);

                Assert(success);
            }

            return true;
        }

        public bool TryRemove(int seqNum)
        {
            return TryRemove(seqNum, out _, out _, out _); // TODO
        }

        /// <summary>
        /// Updates all outgoing messages of a single receiver and returns the updated messages.
        /// </summary>
        /// <param name="address">The address, the messages were sent to originally.</param>
        /// <param name="updatedAddress">The new receiver address.</param>
        /// <returns>A collection of all updated messages.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="address"/> or <paramref name="updatedAddress"/> is null.
        /// </exception>
        public IEnumerable<(int seqNum, ReadOnlyMemory<byte> bytes)> Update(string address, string updatedAddress)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (updatedAddress == null)
                throw new ArgumentNullException(nameof(updatedAddress));

            Dictionary<int, ReadOnlyMemory<byte>> messages;

            lock (_lock)
            {
                if (!_byAddress.Remove(address, out messages))
                {
                    return Enumerable.Empty<(int seqNum, ReadOnlyMemory<byte> bytes)>();
                }

                if (!_byAddress.TryGetValue(updatedAddress, out var updatedMessages))
                {
                    updatedMessages = new Dictionary<int, ReadOnlyMemory<byte>>();
                    _byAddress.Add(address, updatedMessages);
                }

                foreach (var message in messages)
                {
                    var success = updatedMessages.TryAdd(message.Key, message.Value);
                    Assert(success);

                    success = _bySeqNum.TryGetValue(message.Key, out var entry);
                    Assert(success);

                    if (success)
                    {
                        _bySeqNum[message.Key] = (updatedAddress, entry.bytes, entry.ackSource);
                    }
                }
            }

            return messages.Select(p => (seqNum: p.Key, bytes: p.Value));
        }
    }
}
