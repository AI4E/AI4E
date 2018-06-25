using System;

namespace AI4E.Storage.Sample.Domain
{
    public readonly struct ProductName : IEquatable<ProductName>
    {
        public ProductName(string value)
        {
            if (!IsValid(value, out var message))
                throw new ArgumentException(message, nameof(value));

            Value = value;
        }

        public string Value { get; }

        public static bool IsValid(string value, out string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                message = "The argument must be a non-empty string that does not consist of whitespace only.";
                return false;
            }

            message = default;
            return true;
        }

        public static explicit operator ProductName(string value)
        {
            return new ProductName(value);
        }

        public static implicit operator string(ProductName productName)
        {
            return productName.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ProductName productName && Equals(productName);
        }

        public bool Equals(ProductName other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ProductName left, ProductName right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ProductName left, ProductName right)
        {
            return !left.Equals(right);
        }
    }
}
