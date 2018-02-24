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

            var id = (Guid)value.GetType().GetProperty("Id").GetValue(value);

            writer.WriteValue(id.ToString()); // TODO: Use SGuid
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (!CanConvert(objectType))
                return serializer.Deserialize(reader, objectType);

            var id = new Guid(reader.Value as string); // TODO: Use SGuid

            var referencedType = objectType.GetGenericArguments()[0];

            var ci = typeof(Reference<>).MakeGenericType(referencedType)
                                        ?.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, 
                                                         binder: null, 
                                                         types: new[] { typeof(Guid), typeof(IReferenceResolver) },
                                                         modifiers: null);

            if(ci == null)
            {
                throw new Exception(); // TODO
            }

            return ci.Invoke(new object[] { id, _referenceResolver });
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Reference<>);
        }
    }
}
