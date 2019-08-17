using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination.Caching
{
    public interface ICacheEntry
    {
        string Key { get; }

        bool TryGetValue(out CacheEntryValue value);

        ValueTask<CacheEntryValue> GetValueAsync(CancellationToken cancellation);
        ValueTask InvalidateAsync(CancellationToken cancellation);
        ValueTask<LockedEntry> LockAsync(LockType lockType, CancellationToken cancellation);
    }

    public readonly struct CacheEntryValue : IEquatable<CacheEntryValue>
    {
        private readonly ReadOnlyMemory<byte> _value;

        public CacheEntryValue(ReadOnlyMemory<byte> value)
        {
            IsExisting = true;
            _value = value;
        }

        public bool IsExisting { get; }
        public ReadOnlyMemory<byte> Value => !IsExisting ? ReadOnlyMemory<byte>.Empty : _value;

        public bool Equals(CacheEntryValue other)
        {
            return other.IsExisting == IsExisting &&
                other.Value.Span.SequenceEqual(Value.Span);
        }

        public override bool Equals(object obj)
        {
            return obj is CacheEntryValue cacheEntryValue && Equals(cacheEntryValue);
        }

        public override int GetHashCode()
        {
            return (IsExisting, Value).GetHashCode();
        }

        public static bool operator ==(CacheEntryValue left, CacheEntryValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CacheEntryValue left, CacheEntryValue right)
        {
            return !left.Equals(right);
        }

    }
}
