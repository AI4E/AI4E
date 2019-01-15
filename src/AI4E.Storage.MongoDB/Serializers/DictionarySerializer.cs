using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.MongoDB.Serializers
{
    public sealed class DictionarySerializerProvider : IBsonSerializationProvider
    {
        private static readonly Type _serializerTypeDefinition = typeof(DictionarySerializer<,,>);

        private readonly ThreadLocal<Dictionary<Type, IBsonSerializer>> _serializers;

        public DictionarySerializerProvider()
        {
            _serializers = new ThreadLocal<Dictionary<Type, IBsonSerializer>>(
                valueFactory: () => new Dictionary<Type, IBsonSerializer>(),
                trackAllValues: false);
        }

        private Dictionary<Type, IBsonSerializer> Serializers => _serializers.Value;

        public IBsonSerializer GetSerializer(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not specify an open type definition.", nameof(type));

            if (!CanHandle(type))
                return null;

            if (!Serializers.TryGetValue(type, out var serializer))
            {
                serializer = CreateSerializer(type);

                Serializers.Add(type, serializer);
            }

            return serializer;
        }

        private bool CanHandle(Type type)
        {
            return type.GetInterfaces().Any(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IDictionary<,>)) &&
                   type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, modifiers: null) != null;
        }

        private IBsonSerializer CreateSerializer(Type type)
        {
            Assert(type != null);
            Assert(_serializerTypeDefinition != null);
            var implementedInterface = type.GetInterfaces().FirstOrDefault(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            Assert(implementedInterface != null);

            var types = implementedInterface.GetGenericArguments();

            Assert(types != null && types.Length == 2);

            var keyType = types[0];
            var valueType = types[1];
            var serializerType = _serializerTypeDefinition.MakeGenericType(type, keyType, valueType);
            var serializer = Activator.CreateInstance(serializerType) as IBsonSerializer;
            Assert(serializer != null);

            return serializer;
        }
    }

    public sealed class DictionarySerializer<TDictionary, TKey, TValue> : SerializerBase<TDictionary>
        where TDictionary : IDictionary<TKey, TValue>, new()
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TDictionary value)
        {
            var bsonWriter = context.Writer;

            if (typeof(TKey) == typeof(string))
            {
                bsonWriter.WriteStartDocument();
                foreach (var kvp in value)
                {
                    bsonWriter.WriteName(Unsafe.As<string>(kvp.Key));
                    BsonSerializer.Serialize(bsonWriter, kvp.Value, args: args);
                }
                bsonWriter.WriteEndDocument();
            }
            else
            {
                bsonWriter.WriteStartArray();

                foreach (var kvp in value)
                {
                    bsonWriter.WriteStartDocument();

                    bsonWriter.WriteName("key");
                    BsonSerializer.Serialize(bsonWriter, kvp.Key, args: args);
                    bsonWriter.WriteName("value");
                    BsonSerializer.Serialize(bsonWriter, kvp.Value, args: args);

                    bsonWriter.WriteEndDocument();
                }

                bsonWriter.WriteEndArray();
            }
        }

        public override TDictionary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;

            var result = new TDictionary();

            if (typeof(TKey) == typeof(string))
            {
                bsonReader.ReadStartDocument();
                while (bsonReader.ReadBsonType() != BsonType.EndOfDocument)
                {
                    var key = bsonReader.ReadName();
                    var value = BsonSerializer.Deserialize<TValue>(bsonReader);

                    result.Add(Unsafe.As<string, TKey>(ref key), value);
                }
                bsonReader.ReadEndDocument();
            }
            else
            {
                bsonReader.ReadStartArray();
                while (bsonReader.ReadBsonType() == BsonType.Document)
                {
                    bsonReader.ReadStartDocument();

                    TKey key = default;
                    TValue value = default;
                    bool keySet = false, valueSet = false;
                    var name = bsonReader.ReadName();


                    switch (name)
                    {
                        case "key":
                            if (keySet)
                            {
                                throw new Exception(); // TODO
                            }

                            key = BsonSerializer.Deserialize<TKey>(bsonReader);
                            keySet = true;
                            break;

                        case "value":
                            if (valueSet)
                            {
                                throw new Exception(); // TODO
                            }

                            value = BsonSerializer.Deserialize<TValue>(bsonReader);
                            valueSet = true;
                            break;

                        default:
                            throw new Exception(); // TODO
                    }

                    if (!keySet || !valueSet)
                    {
                        throw new Exception(); // TODO
                    }

                    result.Add(key, value);

                    bsonReader.ReadEndDocument();
                }
                bsonReader.ReadEndArray();
            }

            return result;
        }
    }
}
