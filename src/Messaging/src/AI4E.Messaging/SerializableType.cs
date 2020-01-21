using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace AI4E.Messaging
{
    // TODO: Move to Utils
    [Serializable]
    public readonly struct SerializableType : IEquatable<SerializableType>, ISerializable
    {
        private readonly Type? _type;
        private readonly string? _typeName;

        public SerializableType(string typeName, Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (typeName is null)
                throw new ArgumentNullException(nameof(typeName));

            _type = type;
            _typeName = typeName;
        }

        public SerializableType(string typeName)
        {
            if (typeName is null)
                throw new ArgumentNullException(nameof(typeName));

            _typeName = typeName;
            _type = null;
        }

        private SerializableType(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            if (serializationInfo is null)
                throw new ArgumentNullException(nameof(serializationInfo));

            _typeName = serializationInfo.GetString(nameof(TypeName));
            _type = null;
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(TypeName), _typeName);
        }

        public string TypeName => _typeName ?? typeof(object).GetUnqualifiedTypeName();

        public bool TryGetType([NotNullWhen(true)] out Type? type)
        {
            type = _type;
            return !(type is null);
        }

        public bool Equals(in SerializableType other)
        {
            return other.TypeName.Equals(TypeName, StringComparison.Ordinal);
        }

        public bool Equals(SerializableType other)
        {
            return Equals(in other);
        }

        public override bool Equals(object? obj)
        {
            return obj is SerializableType serializableType && Equals(in serializableType);
        }

        public override int GetHashCode()
        {
            return TypeName.GetHashCode(StringComparison.Ordinal);
        }

        public static bool operator ==(in SerializableType left, in SerializableType right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in SerializableType left, in SerializableType right)
        {
            return !left.Equals(right);
        }
    }
}
