using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace AI4E.Messaging
{
    [JsonConverter(typeof(SerializableTypeConverter))]
    public readonly struct SerializableType : IEquatable<SerializableType>
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

    public sealed class SerializableTypeConverter : JsonConverter<SerializableType?>
    {
        public override void WriteJson(
            JsonWriter writer,
            SerializableType? value,
            JsonSerializer serializer)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            if (value is null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(((SerializableType)value).TypeName);
            }
        }

        public override SerializableType? ReadJson(
            JsonReader reader,
            Type objectType,
            SerializableType? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            if (serializer is null)
                throw new ArgumentNullException(nameof(serializer));

            if (reader.Value is null)
            {
                return null;
            }

            if (reader.Value is string typeName)
            {
                var type = serializer.SerializationBinder.BindToType(assemblyName: null, typeName);

                return new SerializableType(typeName, type);
            }

            throw new JsonSerializationException();
        }
    }
}
