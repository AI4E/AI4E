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
