using System;

namespace AI4E.Storage.Sample.Domain
{
    public readonly struct Price : IEquatable<Price>
    {
        public Price(decimal value)
        {
            if (!IsValid(value, out var message))
                throw new ArgumentException(message, nameof(value));

            Value = value;
        }

        public decimal Value { get; }

        public static bool IsValid(decimal value, out string message)
        {
            if (value < 0)
            {
                message = "The argument must be a positive value or zero.";
                return false;
            }

            message = default;
            return true;
        }

        public static explicit operator Price(decimal value)
        {
            return new Price(value);
        }

        public static implicit operator decimal(Price price)
        {
            return price.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is Price price && Equals(price);
        }

        public bool Equals(Price other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(Price left, Price right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Price left, Price right)
        {
            return !left.Equals(right);
        }
    }
}
