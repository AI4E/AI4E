using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.FrontEnd
{
    // TODO: Can the locking be more fine grained or lock-free algorithms beeing used instead?
    public sealed class OutboundMessageLookup
    {
        private readonly object _lock = new object();
        private readonly Dictionary<int, (string address, byte[] bytes, TaskCompletionSource<object> ackSource)> _bySeqNum;
        private readonly Dictionary<string, Dictionary<int, byte[]>> _byAddress;

        public OutboundMessageLookup()
        {
            _bySeqNum = new Dictionary<int, (string address, byte[] bytes, TaskCompletionSource<object> ackSource)>();
            _byAddress = new Dictionary<string, Dictionary<int, byte[]>>();
        }

        public bool TryAdd(int seqNum, string address, byte[] bytes, TaskCompletionSource<object> ackSource)
        {
            if (address == null)
                throw new System.ArgumentNullException(nameof(address));

            if (bytes == null)
                throw new System.ArgumentNullException(nameof(bytes));

            if (ackSource == null)
                throw new System.ArgumentNullException(nameof(ackSource));

            lock (_lock)
            {
                if (!_bySeqNum.TryAdd(seqNum, (address, bytes, ackSource)))
                    return false;

                if (!_byAddress.TryGetValue(address, out var messages))
                {
                    messages = new Dictionary<int, byte[]>();
                    _byAddress.Add(address, messages);
                }

                var success = messages.TryAdd(seqNum, bytes);

                Assert(success);
            }

            return true;
        }

        public bool TryRemove(int seqNum, out string address, out byte[] bytes, out TaskCompletionSource<object> ackSource)
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

        public IEnumerable<(int seqNum, byte[] bytes)> Update(string address, string updatedAddress)
        {
            if (address == null)
                throw new System.ArgumentNullException(nameof(address));

            if (updatedAddress == null)
                throw new System.ArgumentNullException(nameof(updatedAddress));

            Dictionary<int, byte[]> messages;

            lock (_lock)
            {
                if (!_byAddress.Remove(address, out messages))
                {
                    return Enumerable.Empty<(int seqNum, byte[] bytes)>();
                }

                if (!_byAddress.TryGetValue(updatedAddress, out var updatedMessages))
                {
                    updatedMessages = new Dictionary<int, byte[]>();
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
