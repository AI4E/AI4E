using System;
using System.Collections.Generic;
using AI4E.Utils;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace AI4E.Storage.MongoDB
{
    internal sealed class DiscriminatorConvention : IDiscriminatorConvention
    {
        public static DiscriminatorConvention Instance { get; } = new DiscriminatorConvention();

        public Type GetActualType(IBsonReader bsonReader, Type nominalType)
        {
            var result = nominalType;

            var bookmark = bsonReader.GetBookmark();
            bsonReader.ReadStartDocument();
            if (bsonReader.FindElement(ElementName))
            {
                var value = bsonReader.ReadString();

                // TODO: Always use the default resolver?
                var resolvedType = TypeResolver.Default.ResolveType(value);

                if (!nominalType.IsAssignableFrom(resolvedType))
                    throw new InvalidOperationException(
                        $"Database type does not inherit nominal type { nominalType.GetUnqualifiedTypeName()}.");

                result = resolvedType;
            }

            bsonReader.ReturnToBookmark(bookmark);
            return result;
        }

        public BsonValue GetDiscriminator(Type nominalType, Type actualType)
        {
            return actualType.GetUnqualifiedTypeName();
        }

        public string ElementName => "_t";

        private static readonly HashSet<Type> _registeredTo = new HashSet<Type>();
        private static readonly object _mutex = new object();

        public static void Register(Type nominalType)
        {
            lock (_mutex)
            {
                if (!_registeredTo.Add(nominalType))
                {
                    return;
                }
            }

            try
            {
                BsonSerializer.RegisterDiscriminatorConvention(nominalType, Instance);
            }
            catch
            {
                lock (_mutex)
                {
                    _registeredTo.Remove(nominalType);
                }

                throw;
            }
        }
    }
}
