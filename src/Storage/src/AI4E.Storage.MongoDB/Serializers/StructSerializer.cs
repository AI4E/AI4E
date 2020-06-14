/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Diagnostics;

namespace AI4E.Storage.MongoDB.Serializers
{
#nullable disable

    // Based on: 
    // https://stackoverflow.com/questions/16501145/serializing-immutable-value-types-with-mongo-c-sharp-driver#answer-39613579
    // https://stackoverflow.com/questions/26788855/how-do-you-serialize-value-types-with-mongodb-c-sharp-serializer#answer-38911049
    public sealed class StructSerializationProvider : IBsonSerializationProvider
    {
        private static readonly Type _serializerTypeDefinition = typeof(StructSerializer<>);

        private readonly ThreadLocal<Dictionary<Type, IBsonSerializer>> _serializers;

        public StructSerializationProvider()
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
            Debug.Assert(type != null);

            if (!type.IsValueType)
                return false;

            if (type.IsEnum)
                return false;

            if (type == typeof(char))
                return false;

            if (type == typeof(bool))
                return false;

            if (type == typeof(byte))
                return false;

            if (type == typeof(sbyte))
                return false;

            if (type == typeof(ushort))
                return false;

            if (type == typeof(short))
                return false;

            if (type == typeof(uint))
                return false;

            if (type == typeof(int))
                return false;

            if (type == typeof(ulong))
                return false;

            if (type == typeof(long))
                return false;

            if (type == typeof(float))
                return false;

            if (type == typeof(double))
                return false;

            if (type == typeof(decimal))
                return false;

            if (type == typeof(DateTime))
                return false;

            if (type == typeof(TimeSpan))
                return false;

            if (type == typeof(Guid))
                return false;

            if (IsKeyValuePair(type))
                return false;

            // TODO: Do we support no nullable types in general or just nullable types of the primitives above?
            if (IsNullable(type))
                return false;

            return true;
        }

        private static bool IsKeyValuePair(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
        }

        private static bool IsNullable(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private IBsonSerializer CreateSerializer(Type type)
        {
            Debug.Assert(type != null);
            Debug.Assert(type.IsValueType);
            Debug.Assert(_serializerTypeDefinition != null);

            var serializerType = _serializerTypeDefinition.MakeGenericType(type);
            var serializer = Activator.CreateInstance(serializerType) as IBsonSerializer;

            Debug.Assert(serializer != null);

            return serializer;
        }
    }

    public sealed class StructSerializer<T> : StructSerializerBase<T> where T : struct
    {
        private static readonly PropertyInfo[] _emtptyProperties = Array.Empty<PropertyInfo>();

        private readonly BindingFlags _bindingFlags;
        private readonly IImmutableSet<PropertyInfo> _properties;
        private readonly ConstructorInfo _constructor;

        #region C'tor

        public StructSerializer(BindingFlags bindingFlags)
        {
            _bindingFlags = bindingFlags | BindingFlags.DeclaredOnly;

            var type = typeof(T);

            var readOnlyProperties = type.GetProperties(_bindingFlags)
                                         .Where(p => IsReadOnlyProperty(p))
                                         .ToList();

            IReadOnlyCollection<PropertyInfo> properties = null;

            foreach (var constructor in type.GetConstructors())
            {
                // If we found a matching constructor then we map it and all the readonly properties
                var matchingProperties = GetMatchingProperties(constructor, readOnlyProperties);

                if (!matchingProperties.Any())
                {
                    if (_constructor == null)
                    {
                        _constructor = constructor;
                        properties = _emtptyProperties;
                    }

                    continue;
                }

                if (properties != null && properties.Count >= matchingProperties.Count)
                {
                    continue;
                }

                // Map constructor
                _constructor = constructor;

                // Map properties
                properties = matchingProperties;

                // We could match all properties
                if (properties.Count == readOnlyProperties.Count)
                {
                    break;
                }
            }

            Debug.Assert(_constructor != null);
            Debug.Assert(properties != null);

            _properties = properties.ToImmutableHashSet();

            //if (properties.Count != readOnlyProperties.Count)
            //{
            //    var unmatchedProperties = readOnlyProperties.Except(_properties);

            //    var message = $"The type '{type.FullName}' cannot be serialized.";

            //    if (readOnlyProperties.Count - properties.Count == 1)
            //    {
            //        message += $" Property '{unmatchedProperties.First().Name}' cannot be deserialized.";
            //    }
            //    else
            //    {
            //        message += $" Properties '{unmatchedProperties.Select(p => p.Name).Aggregate((e, n) => e + ", " + n)}' cannot be deserialized.";
            //    }

            //    throw new FormatException(message);
            //}
        }

        public StructSerializer() : this(BindingFlags.Instance | BindingFlags.Public) { }

        #endregion

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
        {
            var bsonWriter = context.Writer;
            var type = typeof(T);
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                 .Where(property => property.CanWrite);

            bsonWriter.WriteStartDocument();

            foreach (var property in _properties)
            {
                bsonWriter.WriteName(property.Name);
                BsonSerializer.Serialize(bsonWriter, property.PropertyType, property.GetValue(value, null));
            }

            foreach (var property in properties)
            {
                Debug.Assert(!_properties.Contains(property));

                bsonWriter.WriteName(property.Name);
                BsonSerializer.Serialize(bsonWriter, property.PropertyType, property.GetValue(value, null));
            }

            foreach (var field in fields)
            {
                bsonWriter.WriteName(field.Name);
                BsonSerializer.Serialize(bsonWriter, field.FieldType, field.GetValue(value));
            }

            bsonWriter.WriteEndDocument();
        }

        public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;
            var type = typeof(T);
            var constructorParameters = _constructor.GetParameters();
            var constructorArguments = new object[constructorParameters.Length];
            var result = default(object);
            var missingConstructorArgumentCount = constructorParameters.Length;
            var delayedExecution = default(Action<object>);

            bsonReader.ReadStartDocument();

            while (bsonReader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var name = bsonReader.ReadName();

                if (missingConstructorArgumentCount == 0)
                {
                    Debug.Assert(result != null);

                    var field = type.GetField(name);
                    if (field != null)
                    {
                        var value = BsonSerializer.Deserialize(bsonReader, field.FieldType);
                        field.SetValue(result, value);

                        continue;
                    }

                    var prop = type.GetProperty(name);
                    if (prop != null)
                    {
                        var value = BsonSerializer.Deserialize(bsonReader, prop.PropertyType);
                        prop.SetValue(result, value, null);

                        continue;
                    }
                }
                else if (MatchParameterOrDelay(constructorParameters,
                                              constructorArguments,
                                              ref missingConstructorArgumentCount,
                                              ref delayedExecution,
                                              ref result,
                                              name,
                                              bsonReader))
                {
                    continue;
                }

                throw new FormatException($"Unable to deserialize object of type {type.FullName}. The element '{name}' cannot be mapped to a member.");
            }

            bsonReader.ReadEndDocument();

            return (T)result;
        }

        private bool MatchParameterOrDelay(ParameterInfo[] constructorParameters,
                                           object[] constructorArguments,
                                           ref int missingConstructorArgumentCount,
                                           ref Action<object> delayedExecution,
                                           ref object result,
                                           string name,
                                           IBsonReader bsonReader)
        {

            var type = typeof(T);
            for (var i = 0; i < constructorParameters.Length; i++)
            {
                var parameter = constructorParameters[i];

                if (!string.Equals(name, parameter.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                var value = BsonSerializer.Deserialize(bsonReader, parameter.ParameterType);
                constructorArguments[i] = value;

                if (--missingConstructorArgumentCount == 0)
                {
                    result = _constructor.Invoke(constructorArguments);
                    delayedExecution?.Invoke(result);
                }

                return true;
            }

            if (type.GetField(name) is var field && field != null)
            {
                var value = BsonSerializer.Deserialize(bsonReader, field.FieldType);

                delayedExecution += obj => field.SetValue(obj, value);

                return true;
            }

            if (type.GetProperty(name) is var prop && prop != null)
            {
                var value = BsonSerializer.Deserialize(bsonReader, prop.PropertyType);

                delayedExecution += obj => prop.SetValue(obj, value, null);

                return true;
            }

            return false;
        }

        private static bool IsReadOnlyProperty(PropertyInfo property)
        {
            // Property cannot be read
            if (!property.CanRead)
                return false;

            // Property is writable
            if (property.CanWrite)
                return false;

            // Property is an indexer
            if (property.GetIndexParameters().Length != 0)
                return false;

            // TODO: as we only handle structs, this may actually never happen.

            // Property is overriden
            var getMethodInfo = property.GetMethod;
            if (getMethodInfo.IsVirtual && getMethodInfo.GetBaseDefinition().DeclaringType != typeof(T))
                return false;

            return true;
        }

        private static IReadOnlyCollection<PropertyInfo> GetMatchingProperties(ConstructorInfo constructor, IEnumerable<PropertyInfo> properties)
        {
            var matchingProperties = new List<PropertyInfo>();

            var ctorParameters = constructor.GetParameters();
            foreach (var ctorParameter in ctorParameters)
            {
                var matchingProperty = properties.FirstOrDefault(p => IsMatchingProperty(ctorParameter, p));

                if (matchingProperty == null)
                {
                    matchingProperties.Clear();
                    break;
                }

                matchingProperties.Add(matchingProperty);
            }

            return matchingProperties;
        }

        private static bool IsMatchingProperty(ParameterInfo parameter, PropertyInfo property)
        {
            return string.Equals(property.Name, parameter.Name, StringComparison.InvariantCultureIgnoreCase) &&
                   parameter.ParameterType.IsAssignableFrom(property.PropertyType);
        }
    }

#nullable restore
}
