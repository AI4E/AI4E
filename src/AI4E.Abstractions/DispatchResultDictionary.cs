using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json;

namespace AI4E
{
    [JsonConverter(typeof(DispatchResultDictionaryConverter))]
    public abstract class DispatchResultDictionary : IReadOnlyDictionary<string, object>, IDispatchResult
    {
        private readonly ImmutableDictionary<string, object> _data;

        private protected DispatchResultDictionary(IDispatchResult dispatchResult,
                                                   Type dispatchResultType,
                                                   IEnumerable<KeyValuePair<string, object>> data)
        {
            if (dispatchResult == null)
                throw new ArgumentNullException(nameof(dispatchResult));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            DispatchResult = dispatchResult;
            _data = data.ToImmutableDictionary();
        }

        public IDispatchResult DispatchResult { get; }
        public Type DispatchResultType { get; }

        #region IDispatchResult

        public bool IsSuccess => DispatchResult.IsSuccess;

        public string Message => DispatchResult.Message;

        #endregion

        #region IReadOnlyDictionary<string, object>

        public object this[string key]
        {
            get
            {
                // Do not pass through to _data as we do not want to throw a KeyNotFoundException
                if (key == null || _data == null)
                {
                    return null;
                }

                if (!_data.TryGetValue(key, out var result))
                {
                    result = null;
                }

                return result;
            }
        }

        public IEnumerable<string> Keys => _data?.Keys ?? Enumerable.Empty<string>();

        public IEnumerable<object> Values => _data?.Values ?? Enumerable.Empty<object>();

        public int Count => _data?.Count ?? 0;

        public bool ContainsKey(string key)
        {
            return key != null && _data != null && _data.ContainsKey(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            if (key == null || _data == null)
            {
                value = default;
                return false;
            }

            return _data.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var enumerable = _data as IEnumerable<KeyValuePair<string, object>> ?? Enumerable.Empty<KeyValuePair<string, object>>();

            return enumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }

    [JsonConverter(typeof(DispatchResultDictionaryConverter))]
    public sealed class DispatchResultDictionary<TDispatchResult> : DispatchResultDictionary
        where TDispatchResult : IDispatchResult
    {
        public DispatchResultDictionary(TDispatchResult dispatchResult)
            : base(dispatchResult, typeof(TDispatchResult), ImmutableDictionary<string, object>.Empty)
        { }

        public DispatchResultDictionary(TDispatchResult dispatchResult, IEnumerable<KeyValuePair<string, object>> data)
            : base(dispatchResult, typeof(TDispatchResult), data)
        { }

        public new TDispatchResult DispatchResult => (TDispatchResult)base.DispatchResult;
    }

    public sealed class DispatchResultDictionaryConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is DispatchResultDictionary dispatchResult))
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            // Write message type
            writer.WritePropertyName("dispatch-result-type");
            serializer.Serialize(writer, dispatchResult.DispatchResultType, typeof(Type));

            // Write message
            writer.WritePropertyName("dispatch-result");
            serializer.Serialize(writer, dispatchResult.DispatchResult, typeof(IDispatchResult));

            // Write data
            if (dispatchResult.Any())
            {
                writer.WritePropertyName("data");
                writer.WriteStartObject();

                foreach (var kvp in dispatchResult)
                {
                    writer.WritePropertyName(kvp.Key);
                    serializer.Serialize(writer, kvp.Value, typeof(object));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (!CanConvert(objectType))
                throw new InvalidOperationException();

            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType != JsonToken.StartObject)
                throw new InvalidOperationException();


            var dispatchResultType = objectType.IsGenericTypeDefinition ? objectType.GetGenericArguments().First() : null;
            object dispatchResult = null;
            ImmutableDictionary<string, object>.Builder data = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                else if (reader.TokenType == JsonToken.PropertyName)
                {
                    if ((string)reader.Value == "dispatch-result-type")
                    {
                        reader.Read();
                        var deserializedMessageType = (Type)serializer.Deserialize(reader, typeof(Type));

                        if (dispatchResultType != null && dispatchResultType != deserializedMessageType)
                        {
                            throw new InvalidOperationException();
                        }

                        dispatchResultType = deserializedMessageType;
                    }
                    else if ((string)reader.Value == "dispatch-result")
                    {
                        reader.Read();
                        dispatchResult = serializer.Deserialize(reader, typeof(object));
                    }
                    else if ((string)reader.Value == "data")
                    {
                        data = ReadData(reader, serializer);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            if (dispatchResultType == null || dispatchResult == null)
                throw new InvalidOperationException();

            var resultType = typeof(DispatchResultDictionary<>).MakeGenericType(dispatchResultType);

            return Activator.CreateInstance(resultType, dispatchResult, data?.ToImmutable() ?? ImmutableDictionary<string, object>.Empty);
        }

        private ImmutableDictionary<string, object>.Builder ReadData(JsonReader reader, JsonSerializer serializer)
        {
            var result = ImmutableDictionary.CreateBuilder<string, object>();

            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                return null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    return result;
                }
                else if (reader.TokenType == JsonToken.PropertyName)
                {
                    var key = (string)reader.Value;
                    reader.Read();
                    var value = serializer.Deserialize(reader, typeof(object));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            return result;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DispatchResultDictionary) ||
                   objectType.IsGenericType &&
                   objectType.GetGenericTypeDefinition() == typeof(DispatchResultDictionary<>);
        }
    }
}
