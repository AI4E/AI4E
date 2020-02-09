﻿using AI4E.Internal;
using AI4E.Storage.MongoDB.Serializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace AI4E.Storage.MongoDB
{
    public partial class MongoDatabase
    {
        static MongoDatabase()
        {
            BsonSerializer.RegisterSerializationProvider(new StructSerializationProvider());
            BsonSerializer.RegisterSerializationProvider(new DictionarySerializerProvider());
            var conventionPack = new ConventionPack
            {
                new ClassMapConvention()
            };
            ConventionRegistry.Register("AI4E default convention pack", conventionPack, _ => true);
        }

        private sealed class ClassMapConvention : IClassMapConvention
        {
            public string Name => typeof(ClassMapConvention).ToString();

            public void Apply(BsonClassMap classMap)
            {
                var idMember = DataPropertyHelper.GetIdMember(classMap.ClassType);

                if (idMember != null)
                {
                    classMap.MapIdMember(idMember);
                }
            }
        }
    }
}