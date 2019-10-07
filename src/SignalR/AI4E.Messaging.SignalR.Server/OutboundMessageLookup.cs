using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Messaging.SignalR.Server
{
    /// <summary>
    /// A two way lookup for outgoing messages, storing messages until acknowledged.
    /// </summary>
    /// <remarks>
    /// This type is thread-safe.
    /// </remarks>
    public sealed class OutboundMessageLookup
    {
        private readonly object _lock = new object();

        // Stores messages indexed by the message seq-num. (One per seq-num)
        private readonly Dictionary<int, (RouteEndPointAddress endPoint, ValueMessage message, TaskCompletionSource<object> ackSource)> _bySeqNum;

        // Stored messages indexed by the receiver end-point. (Multiple per address)
        private readonly Dictionary<RouteEndPointAddress, Dictionary<int, ValueMessage>> _byAddress;

        /// <summary>
        /// Creates a new instance of the <see cref="OutboundMessageLookup"/> type.
        /// </summary>
        public OutboundMessageLookup()
        {
            _bySeqNum = new Dictionary<int, (RouteEndPointAddress endPoint, ValueMessage bytes, TaskCompletionSource<object> ackSource)>();
            _byAddress = new Dictionary<RouteEndPointAddress, Dictionary<int, ValueMessage>>();
        }

        /// <summary>
        /// Tries to add an outgoing message to the lookup.
        /// </summary>
        /// <param name="seqNum">The seq-num of the outging message.</param>
        /// <param name="endPoint">The end-point of the message receiver.</param>
        /// <param name="bytes">The message payload.</param>
        /// <param name="ackSource">The transmission operations task source</param>
        /// <returns>True if the message was added successfully, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="endPoint"/> or <paramref name="ackSource"/> is null.
        /// </exception>
        public bool TryAdd(int seqNum, RouteEndPointAddress endPoint, ValueMessage message, TaskCompletionSource<object> ackSource)
        {
            if (endPoint == default)
            {
                throw new ArgumentDefaultException(nameof(endPoint));
            }

            if (ackSource == null)
            {
                throw new ArgumentNullException(nameof(ackSource));
            }

            lock (_lock)
            {
                if (!_bySeqNum.TryAdd(seqNum, (endPoint, message, ackSource)))
                {
                    return false;
                }

                if (!_byAddress.TryGetValue(endPoint, out var messages))
                {
                    messages = new Dictionary<int, ValueMessage>();
                    _byAddress.Add(endPoint, messages);
                }

                var success = messages.TryAdd(seqNum, message);

                Debug.Assert(success);
            }

            return true;
        }

        /// <summary>
        /// Tries to remove the message with the specified seq-num from the lookup.
        /// </summary>
        /// <param name="seqNum">The messages seq-num.</param>
        /// <param name="endPoint">Contains the receiver end-point, if the removal was successfull.</param>
        /// <param name="bytes">Contains the messages payload, if the removal was successfull.</param>
        /// <param name="ackSource">Contains the transmission operations task source, if the removal was successfull.</param>
        /// <returns>True if the message was removed successfully, false otherwise.</returns>
        public bool TryRemove(int seqNum, out RouteEndPointAddress endPoint, out ValueMessage message, out TaskCompletionSource<object> ackSource)
        {
            lock (_lock)
            {
                if (!_bySeqNum.Remove(seqNum, out var entry))
                {
                    message = default;
                    endPoint = default;
                    ackSource = default;

                    return false;
                }

                message = entry.message;
                endPoint = entry.endPoint;
                ackSource = entry.ackSource;

                var success = _byAddress.TryGetValue(endPoint, out var messages) &&
                              messages.Remove(seqNum);

                Debug.Assert(success);
            }

            return true;
        }

        public bool TryRemove(int seqNum)
        {
            return TryRemove(seqNum, out _, out _, out _); // TODO
        }

        public IReadOnlyList<(int seqNum, ValueMessage message)> GetAll(RouteEndPointAddress endPoint)
        {
            if (endPoint == default)
            {
                throw new ArgumentDefaultException(nameof(endPoint));
            }

            lock (_lock)
            {
                if (!_byAddress.TryGetValue(endPoint, out var intermediate))
                {
                    return ImmutableList<(int seqNum, ValueMessage message)>.Empty;
                }

                return intermediate.Select(p => (seqNum: p.Key, bytes: p.Value)).ToImmutableList();
            }
        }
    }
}
