/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.IO;
using System.Threading;
using AI4E.Internal;
using AI4E.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AI4E
{
    public static class Serializer
    {
        // Copied from RMD. TODO: Share this code.
        private static readonly ThreadLocal<JsonSerializer> _serializer = new ThreadLocal<JsonSerializer>(BuildSerializer, trackAllValues: false);

        private static JsonSerializer BuildSerializer()
        {
            var result = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new SerializationBinder()
            };

            result.Converters.Add(new TypeConverter());

            return result;
        }

        private sealed class SerializationBinder : ISerializationBinder
        {
            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                typeName = serializedType.GetUnqualifiedTypeName();
                assemblyName = null;
            }

            public Type BindToType(string assemblyName, string typeName)
            {
                return TypeLoadHelper.LoadTypeFromUnqualifiedName(typeName);
            }
        }

        public static T Roundtrip<T>(T t)
        {
            var value = Serialize(t, typeof(T));
            var result = Deserialize(value, typeof(T));

            if (result == null)
                return default;

            return (T)result;
        }

        public static T RoundtripUnknownType<T>(T t)
        {
            var value = Serialize(t, typeof(object));
            var result = Deserialize(value, typeof(object));

            if (result == null)
                return default;

            return (T)result;
        }

        private static string Serialize(object value, Type expectedType)
        {
            var serializer = _serializer.Value;

            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, value, expectedType);

                return stringWriter.ToString();
            }
        }

        private static object Deserialize(string value, Type expectedType)
        {
            var serializer = _serializer.Value;

            using (var stringReader = new StringReader(value))
            {
                return serializer.Deserialize(stringReader, expectedType);
            }
        }
    }
}
