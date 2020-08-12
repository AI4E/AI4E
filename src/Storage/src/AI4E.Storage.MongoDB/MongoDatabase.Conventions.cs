using System;
using AI4E.Internal;
using AI4E.Storage.MongoDB.Serializers;
using AI4E.Utils;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace AI4E.Storage.MongoDB
{
    public partial class MongoDatabase
    {
        static MongoDatabase()
        {
            var typeSerializer = new TypeSerializer();
            BsonSerializer.RegisterSerializer(typeof(Type), typeSerializer);
            BsonSerializer.RegisterSerializer(TypeResolver.Default.ResolveType("System.RuntimeType"), typeSerializer);
            BsonSerializer.RegisterSerializationProvider(new StructSerializationProvider());
            BsonSerializer.RegisterSerializationProvider(new DictionarySerializerProvider());
            var conventionPack = new ConventionPack
            {
                new AI4EMongoDBConvention()
            };
            ConventionRegistry.Register("AI4EMongoDBConventionPack", conventionPack, _ => true);
        }

        private sealed class AI4EMongoDBConvention : IClassMapConvention, IMemberMapConvention
        {
            public string Name => typeof(AI4EMongoDBConvention).ToString();

            public void Apply(BsonClassMap classMap)
            {
                var idMember = DataPropertyHelper.GetIdMember(classMap.ClassType);

                if (idMember != null && idMember.DeclaringType == classMap.ClassType)
                {
                    classMap.MapIdMember(idMember);
                }

                DiscriminatorConvention.Register(classMap.ClassType);
            }

            public void Apply(BsonMemberMap memberMap)
            {
                DiscriminatorConvention.Register(memberMap.MemberType);
            }
        }
    }
}
