using System;

namespace AI4E.Remoting
{
    public readonly struct MultiplexAddress<TAddress> : IEquatable<MultiplexAddress<TAddress>>
    {
        public MultiplexAddress(TAddress address, string application)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (application == null)
                throw new ArgumentNullException(nameof(application));

            Address = address;
            Application = application;
        }

        public TAddress Address { get; }
        public string Application { get; }

        public bool Equals(MultiplexAddress<TAddress> other)
        {
            return Application == other.Application &&
                   Address.Equals(other.Address);
        }

        public override bool Equals(object obj)
        {
            return obj is MultiplexAddress<TAddress> applicationAddress && Equals(applicationAddress);
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode() ^ Application.GetHashCode();
        }

        public static bool operator ==(MultiplexAddress<TAddress> left, MultiplexAddress<TAddress> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MultiplexAddress<TAddress> left, MultiplexAddress<TAddress> right)
        {
            return !left.Equals(right);
        }
    }
}
