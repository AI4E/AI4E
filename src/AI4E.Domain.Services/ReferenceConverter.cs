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
using System.Reflection;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Domain.Services
{
    public sealed class ReferenceConverter : JsonConverter
    {
        private readonly IReferenceResolver _referenceResolver;

        public ReferenceConverter(IReferenceResolver referenceResolver)
        {
            if (referenceResolver == null)
                throw new ArgumentNullException(nameof(referenceResolver));

            _referenceResolver = referenceResolver;
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null || !CanConvert(value.GetType()))
            {
                serializer.Serialize(writer, value);
                return;
            }

            var isSnapshot = value.GetType().GetGenericTypeDefinition() == typeof(Snapshot<>);

            if (isSnapshot)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("id");
            }

            var id = (string)value.GetType().GetProperty("Id").GetValue(value);
            writer.WriteValue(id);

            if (isSnapshot)
            {
                writer.WritePropertyName("revision");

                var revision = (long)value.GetType().GetProperty("Revision").GetValue(value);
                writer.WriteValue(revision);

                writer.WriteEndObject();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (!CanConvert(objectType))
                return serializer.Deserialize(reader, objectType);

            var isSnapshot = objectType.GetGenericTypeDefinition() == typeof(Snapshot<>);
            var referencedType = objectType.GetGenericArguments()[0];

            var id = default(string);

            if (isSnapshot)
            {
                var revision = default(long);
                Assert(reader.TokenType == JsonToken.StartObject);
                reader.Read(); // Read start object.

                while (reader.TokenType == JsonToken.EndObject)
                {
                    Assert(reader.TokenType == JsonToken.PropertyName);

                    switch (reader.Value as string)
                    {
                        case "id":
                            reader.Read();
                            id = reader.Value as string;
                            break;

                        case "revision":
                            reader.Read();
                            revision = (long)reader.Value;
                            break;

                        default:
                            throw new Exception("Unknown format");
                    }
                }

                var snapshotCtor = typeof(Snapshot<>).MakeGenericType(referencedType)
                                           ?.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                                                         binder: null,
                                                         types: new[] { typeof(Guid), typeof(long), typeof(IReferenceResolver) },
                                                         modifiers: null);

                if (snapshotCtor == null)
                {
                    throw new Exception(); // TODO
                }

                return snapshotCtor.Invoke(new object[] { id, revision, _referenceResolver });
            }

            id = reader.Value as string;

            var revisionCtor = typeof(Reference<>).MakeGenericType(referencedType)
                                    ?.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                                                     binder: null,
                                                     types: new[] { typeof(Guid), typeof(IReferenceResolver) },
                                                     modifiers: null);

            if (revisionCtor == null)
            {
                throw new Exception(); // TODO
            }

            return revisionCtor.Invoke(new object[] { id, _referenceResolver });
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType && (objectType.GetGenericTypeDefinition() == typeof(Reference<>) || objectType.GetGenericTypeDefinition() == typeof(Snapshot<>));
        }
    }
}
