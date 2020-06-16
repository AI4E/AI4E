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
using Newtonsoft.Json;

namespace AI4E.Internal
{
    internal sealed class TypeConverter : JsonConverter<Type>
    {
        public override void WriteJson(JsonWriter writer, Type? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
            }
            else
            {
                serializer.SerializationBinder.BindToName(value, out _, out var typeName);
                writer.WriteValue(typeName);
            }
        }

        public override Type ReadJson(JsonReader reader, Type objectType, Type? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value is string typeName)
            {
                return serializer.SerializationBinder.BindToType(assemblyName: null, typeName);
            }

            throw new JsonSerializationException();
        }
    }
}
