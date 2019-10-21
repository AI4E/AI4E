using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using AI4E.Utils;

#nullable enable

namespace AI4E.Messaging.Routing
{
    /// <summary>
    /// Represents a message-route that is used to route the message in the messaging-system.
    /// </summary>
    public readonly struct Route : IEquatable<Route>
    {
        private readonly string? _routeTypeName;
        private readonly string? _messageTypeName;

        /// <summary>
        /// Creates a new instance of the <see cref="Route"/> type.
        /// </summary>
        /// <param name="value">The route's string value.</param>
        public Route(Type messageType)
        {
            _messageTypeName = messageType.GetUnqualifiedTypeName();
            _routeTypeName = null;
        }

        public Route(Type messageType, Type routeType)
        {
            _messageTypeName = messageType.GetUnqualifiedTypeName();
            _routeTypeName = routeType.GetUnqualifiedTypeName();
        }

        private Route(string messageTypeName)
        {
            _messageTypeName = messageTypeName;
            _routeTypeName = null;
        }

        private Route(string messageTypeName, string routeTypeName)
        {
            _messageTypeName = messageTypeName;
            _routeTypeName = routeTypeName;
        }

        public bool TryGetMessageType(ITypeResolver typeLoader, [NotNullWhen(true)]out Type? messageType)
        {
            if (typeLoader is null)
                throw new ArgumentNullException(nameof(typeLoader));

            if (_messageTypeName is null)
            {
                messageType = null;
                return false;
            }

            return typeLoader.TryLoadType(_messageTypeName.AsSpan(), out messageType);
        }

        public static Route UnsafeCreateFromString(string str)
        {
            return new Route(str);
        }

        /// <summary>
        /// Returns a string representation of the current route.
        /// </summary>
        /// <returns>A string representation of the current route.</returns>
        public override string ToString()
        {
            return _routeTypeName ?? _messageTypeName ?? string.Empty;
        }

        /// <summary>
        /// Return a hash code for the current route.
        /// </summary>
        /// <returns>A hash code for the current route.</returns>
        public override int GetHashCode()
        {
            return (_routeTypeName ?? _messageTypeName)?.GetHashCode() ?? 0;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Route route && Equals(in route);
        }

        /// <inheritdoc/>
        public bool Equals(Route other)
        {
            return Equals(in other);
        }

        /// <summary>
        /// Indicates whether the current route is equal to another route.
        /// </summary>
        /// <param name="other">A <see cref="Route"/> to compare with the current route.</param>
        /// <returns>True if the current route equals <paramref name="other"/>, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(in Route other)
        {
            return (_routeTypeName ?? _messageTypeName ?? string.Empty) == (other._routeTypeName ?? other._messageTypeName ?? string.Empty);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two routes are equal.
        /// </summary>
        /// <param name="left">The first route.</param>
        /// <param name="right">The second route.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in Route left, in Route right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two routes are inequal.
        /// </summary>
        /// <param name="left">The first route.</param>
        /// <param name="right">The second route.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in Route left, in Route right)
        {
            return !left.Equals(in right);
        }

        public static void Write(BinaryWriter writer, in Route route)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            if (route._messageTypeName is null)
            {
                writer.Write((byte)0);
            }
            else if (route._routeTypeName is null)
            {
                writer.Write((byte)1);
                writer.Write(route._messageTypeName);
            }
            else
            {
                writer.Write((byte)2);
                writer.Write(route._messageTypeName);
                writer.Write(route._routeTypeName);
            }
        }

        public static void Read(BinaryReader reader, out Route route)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            var discriminator = reader.ReadByte();

            if (discriminator == 0)
            {
                route = default;
            }
            else
            {
                var messageTypeName = reader.ReadString();

                if (discriminator == 1)
                {
                    route = new Route(messageTypeName);
                }
                else
                {
                    var routeTypeName = reader.ReadString();
                    route = new Route(messageTypeName, routeTypeName);
                }
            }
        }
    }
}
