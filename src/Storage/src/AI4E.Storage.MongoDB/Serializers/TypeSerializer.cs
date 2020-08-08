/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using AI4E.Utils;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace AI4E.Storage.MongoDB.Serializers
{
    internal sealed class TypeSerializer : IBsonSerializer<Type>
    {
        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Type? value)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (value is null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                var unqualifiedTypeName = value.GetUnqualifiedTypeName();

                context.Writer.WriteString(unqualifiedTypeName);
            }
        }

#pragma warning disable CS8766 // Is the MongoDB assembly correctly nullable annotated?
        public Type? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
#pragma warning restore CS8766 
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;

            }

            var unqualifiedTypeName = context.Reader.ReadString();

            // We resolve the type from the default resolver currently. 
            // TODO: Can we somehow specify which is the current resolver? Maybe use some metadata that flows through??
            return TypeResolver.Default.ResolveType(unqualifiedTypeName);
        }

        void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object? value)
        {
            if (value is null)
            {
                if (context is null)
                    throw new ArgumentNullException(nameof(context));

                context.Writer.WriteNull();
            }
            else if (value is Type type)
            {
                Serialize(context, args, type);
            }
            else
            {
                throw new ArgumentException("The value must be assignable to 'System.Type'", nameof(value));
            }
        }

#pragma warning disable CS8766 // Is the MongoDB assembly correctly nullable annotated?
        object? IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
#pragma warning restore CS8766
        {
            return Deserialize(context, args);
        }

        public Type ValueType => typeof(Type);
    }
}
