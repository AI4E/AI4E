using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace AI4E.Messaging.Serialization
{
    public sealed class DispatchDataDictionaryConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType is null)
                throw new ArgumentNullException(nameof(objectType));

            if (objectType == typeof(DispatchDataDictionary))
                return true;

            if (objectType.IsConstructedGenericType
                && objectType.GetGenericTypeDefinition() == typeof(DispatchDataDictionary<>))
            {
                return true;
            }

            return false;
        }

        public override object? ReadJson(
            JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            if (objectType is null)
                throw new ArgumentNullException(nameof(objectType));

            if (serializer is null)
                throw new ArgumentNullException(nameof(serializer));

            if (!CanConvert(objectType))
            {
                throw new JsonSerializationException($"The converter is not able to handle objects of type {objectType}");
            }

            Type? messageType = null;
            object? message = null;
            ImmutableDictionary<string, object?>? data = null;

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName || !(reader.Value is string propertyName))
                    goto THROW_FORMAT_EXCEPTION;

                if (!reader.Read())
                {
                    goto THROW_FORMAT_EXCEPTION;
                }

                if (propertyName.Equals("MessageType", StringComparison.OrdinalIgnoreCase))
                {
                    if (messageType != null)
                    {
                        goto THROW_FORMAT_EXCEPTION;
                    }

                    messageType = serializer.Deserialize<Type?>(reader);

                    if (messageType == null)
                    {
                        goto THROW_FORMAT_EXCEPTION;
                    }
                }
                else if (propertyName.Equals("Message", StringComparison.OrdinalIgnoreCase))
                {
                    if (message != null)
                    {
                        goto THROW_FORMAT_EXCEPTION;
                    }

                    message = serializer.Deserialize<object?>(reader);

                    if (message == null)
                    {
                        goto THROW_FORMAT_EXCEPTION;
                    }

                }
                else if (propertyName.Equals("Data", StringComparison.OrdinalIgnoreCase))
                {
                    if (data != null)
                    {
                        goto THROW_FORMAT_EXCEPTION;
                    }

                    var builder = ImmutableDictionary.CreateBuilder<string, object?>();

                    while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                    {
                        if (reader.TokenType != JsonToken.PropertyName || !(reader.Value is string itemName))
                            goto THROW_FORMAT_EXCEPTION;

                        if (!reader.Read())
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        if (reader.TokenType == JsonToken.Null)
                        {
                            builder.Add(itemName, null);
                            continue;
                        }

                        if (!reader.Read()
                            || reader.TokenType != JsonToken.PropertyName
                            || !"type".Equals(reader.Value as string, StringComparison.OrdinalIgnoreCase))
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        if (!reader.Read())
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        var type = serializer.Deserialize<Type>(reader);

                        if (type is null)
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        if (!reader.Read()
                            || reader.TokenType != JsonToken.PropertyName
                            || !"value".Equals(reader.Value as string, StringComparison.OrdinalIgnoreCase))
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        if (!reader.Read())
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        var value = serializer.Deserialize(reader, type);

                        if (value is null)
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        if (builder.ContainsKey(itemName))
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }

                        builder.Add(itemName, value);

                        if (!reader.Read() 
                            || reader.TokenType != JsonToken.EndObject)
                        {
                            goto THROW_FORMAT_EXCEPTION;
                        }
                    }

                    data = builder.ToImmutable();
                }
                else
                {
                    goto THROW_FORMAT_EXCEPTION;
                }
            }

            if (messageType != null && message != null)
            {
                try
                {
                    if (data is null)
                    {
                        return DispatchDataDictionary.Create(messageType, message);
                    }

                    return DispatchDataDictionary.Create(messageType, message, data);
                }
                catch (Exception exc)
                {
                    throw new JsonSerializationException("Unable to deserialize dispatch data dictionary.", exc);
                }
            }

        THROW_FORMAT_EXCEPTION:
            throw new JsonSerializationException(
                        "Unable to deserialize dispatch data dictionary. Document format is unknown.");
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            if (serializer is null)
                throw new ArgumentNullException(nameof(serializer));

            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            if (!CanConvert(value.GetType()))
            {
                throw new JsonSerializationException($"The converter is not able to handle objects of type {value.GetType()}");
            }

            var dispatchData = (DispatchDataDictionary)value;

            writer.WriteStartObject();

            writer.WritePropertyName(nameof(dispatchData.MessageType));
            serializer.Serialize(writer, dispatchData.MessageType, typeof(Type));

            writer.WritePropertyName(nameof(dispatchData.Message));
            serializer.Serialize(writer, dispatchData.Message, typeof(object));

            if (dispatchData.Any())
            {
                writer.WritePropertyName("Data");
                writer.WriteStartObject();

                foreach (var kvp in dispatchData)
                {
                    writer.WritePropertyName(kvp.Key);

                    if (kvp.Value is null)
                    {
                        writer.WriteNull();
                        continue;
                    }

                    writer.WriteStartObject();

                    writer.WritePropertyName("type");
                    serializer.Serialize(writer, kvp.Value.GetType(), typeof(Type));

                    writer.WritePropertyName("value");
                    serializer.Serialize(writer, kvp.Value, kvp.Value.GetType());

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }
}
