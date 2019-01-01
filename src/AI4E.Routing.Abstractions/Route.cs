using System;
using System.Runtime.CompilerServices;

namespace AI4E.Routing
{
    public readonly struct Route : IEquatable<Route>
    {
        private readonly string _value;

        public Route(string value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return _value ?? string.Empty;
        }

        public override int GetHashCode()
        {
            return _value?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            return obj is Route route && Equals(in route);
        }

        public bool Equals(Route other)
        {
            return Equals(in other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(in Route other)
        {
            return (_value ?? string.Empty) == (other._value ?? string.Empty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in Route left, in Route right)
        {
            return left.Equals(in right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in Route left, in Route right)
        {
            return !left.Equals(in right);
        }
    }
}
