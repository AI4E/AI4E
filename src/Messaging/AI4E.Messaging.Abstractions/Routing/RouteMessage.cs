
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Messaging.Routing
{
    public readonly struct RouteMessage<TOriginal> : IEquatable<RouteMessage<TOriginal>>
        where TOriginal : class
    {
        private readonly TOriginal? _original;
        private readonly Func<TOriginal, Message>? _serialization;
        private readonly Message _message;

        public RouteMessage(TOriginal original, Func<TOriginal, Message> serialization)
        {
            if (original is null)
                throw new ArgumentNullException(nameof(original));

            if (serialization is null)
                throw new ArgumentNullException(nameof(serialization));

            _original = original;
            _serialization = serialization;
            _message = default;
        }

        public RouteMessage(Message message, TOriginal original)
        {
            if (message == default)
                throw new ArgumentDefaultException(nameof(message));

            if (original is null)
                throw new ArgumentNullException(nameof(original));

            _original = original;
            _serialization = null;
            _message = message;
        }

        public RouteMessage(Message message)
        {
            if (message == default)
                throw new ArgumentDefaultException(nameof(message));

            _original = null;
            _serialization = null;
            _message = message;
        }

        public Message Message
        {
            get
            {
                // If _serialization is available, _original must be available too.
                Debug.Assert(_original != null || _serialization == null);

                return _serialization?.Invoke(_original!) ?? _message;
            }
        }

        public bool TryGetOriginal([NotNullWhen(true)] out TOriginal? original)
        {
            original = _original;
            return original != null;
        }

        public bool Equals(RouteMessage<TOriginal> other)
        {
            return Equals(in other);
        }

        public bool Equals(in RouteMessage<TOriginal> other)
        {
            // Optimize the common case
            if (_message == default && _original is null) // this is the default value.
            {
                return other._message == default && other._original is null;
            }

            return Message == other.Message;
        }

        public override bool Equals(object? obj)
        {
            return obj is RouteMessage<TOriginal> routeMessage &&
                Equals(in routeMessage);
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }

        public static bool operator ==(in RouteMessage<TOriginal> left, in RouteMessage<TOriginal> right)
        {
            return left.Equals(in right);
        }

        public static bool operator !=(in RouteMessage<TOriginal> left, in RouteMessage<TOriginal> right)
        {
            return !left.Equals(in right);
        }
    }
}
