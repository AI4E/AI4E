using System;

namespace AI4E.Modularity.Host
{
    public readonly struct ModuleSourceName : IEquatable<ModuleSourceName>
    {
        public ModuleSourceName(string value)
        {
            if (!IsValid(value, out var message))
                throw new ArgumentException(message, nameof(value));

            Value = value;
        }

        public string Value { get; }

        public bool Equals(ModuleSourceName other)
        {
            return other.Value == Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleSourceName moduleSourceName && Equals(moduleSourceName);
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        public static bool operator ==(in ModuleSourceName left, in ModuleSourceName right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ModuleSourceName left, in ModuleSourceName right)
        {
            return !left.Equals(right);
        }

        public static bool IsValid(string value, out string message)
        {
            // TODO: Validate value

            message = default;
            return true;
        }

        public static explicit operator ModuleSourceName(string name)
        {
            return new ModuleSourceName(name);
        }
    }
}
