
using System;
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Messaging.Routing
{
    public readonly struct RouteMessage<TOriginal>
        where TOriginal : class
    {
        private readonly TOriginal _original;
        private readonly Func<TOriginal, ValueMessage> _serialization;
        private readonly ValueMessage _message;

        public RouteMessage(TOriginal original, Func<TOriginal, ValueMessage> serialization)
        {
            if (original is null)
                throw new ArgumentNullException(nameof(original));

            if (serialization is null)
                throw new ArgumentNullException(nameof(serialization));

            _original = original;
            _serialization = serialization;
            _message = default;
        }

        public RouteMessage(ValueMessage message, TOriginal original)
        {
            if (message == default)
                throw new ArgumentDefaultException(nameof(message));

            if (original is null)
                throw new ArgumentNullException(nameof(original));

            _original = original;
            _serialization = null;
            _message = message;
        }

        public RouteMessage(ValueMessage message)
        {
            if (message == default)
                throw new ArgumentDefaultException(nameof(message));

            _original = null;
            _serialization = null;
            _message = message;
        }

        public ValueMessage Message => _serialization?.Invoke(_original) ?? _message;

        public bool TryGetOriginalMessage(out TOriginal original)
        {
            original = _original;
            return original != null;
        }

        public bool IsDefault()
        {
            return _serialization is null && _message == default;
        }
    }
}
