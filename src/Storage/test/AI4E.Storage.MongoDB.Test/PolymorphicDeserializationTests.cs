using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using MongoDB.Bson.Serialization;
using Xunit;

namespace AI4E.Storage.MongoDB.Test
{
    public sealed class PolymorphicDeserializationTests
    {
        public PolymorphicDeserializationTests()
        {
            // Register the serializer conventions
            try
            {
                new MongoDatabase(null);
            }
            catch { }
        }

        [Fact]
        public void DeserializeAbstractBaseTest()
        {
            // Arrange
            var json = InputJsonLoader.Instance.LoadJsonInput();

            // Act
            var wrapper = BsonSerializer.Deserialize<AbstractBaseWrapper>(json);

            // Assert
            Assert.Collection(wrapper.AbstractBases,
                abstractBaseDerived1 => Assert.IsType<AbstractBaseDerived1>(abstractBaseDerived1),
                abstractBaseDerived2 => Assert.IsType<AbstractBaseDerived2>(abstractBaseDerived2));
        }

        private sealed class AbstractBaseWrapper
        {
            public int Id { get; set; }

            public List<AbstractBase> AbstractBases { get; private set; } = new List<AbstractBase>();
        }

        private abstract class AbstractBase { }

        private sealed class AbstractBaseDerived1 : AbstractBase { }

        private sealed class AbstractBaseDerived2 : AbstractBase { }

        [Fact]
        public void DeserializeBaseTest()
        {
            // Arrange
            var json = InputJsonLoader.Instance.LoadJsonInput();

            // Act
            var wrapper = BsonSerializer.Deserialize<BaseWrapper>(json);

            // Assert
            Assert.Collection(wrapper.Bases,
                baseDerived1 => Assert.IsType<BaseDerived1>(baseDerived1),
                baseDerived2 => Assert.IsType<BaseDerived2>(baseDerived2));
        }

        private sealed class BaseWrapper
        {
            public int Id { get; set; }

            public List<Base> Bases { get; private set; } = new List<Base>();
        }

        private class Base { }

        private sealed class BaseDerived1 : Base { }

        private sealed class BaseDerived2 : Base { }

        [Fact]
        public void DeserializeGenericBaseTest()
        {
            //var baseDerived1 = new GenericBaseDerived1<object>();
            //var baseDerived2 = new GenericBaseDerived2<GenericValue>();
            //var wrapper = new GenericBaseWrapper
            //{
            //    Id = 1,
            //    GenericBases = { baseDerived1, baseDerived2 }
            //};

            //var stringBuilder = new StringBuilder();
            //var stringWriter = new StringWriter(stringBuilder);
            //var jsonWriter = new JsonWriter(stringWriter, new JsonWriterSettings { Indent = true });

            //BsonSerializer.Serialize(jsonWriter, wrapper);

            //var json = stringBuilder.ToString();

            // Arrange
            var json = InputJsonLoader.Instance.LoadJsonInput();

            // Act
            var wrapper = BsonSerializer.Deserialize<GenericBaseWrapper>(json);

            // Assert
            Assert.Collection(wrapper.GenericBases,
                baseDerived1 => Assert.IsType<GenericBaseDerived1<object>>(baseDerived1),
                baseDerived2 => Assert.IsType<GenericBaseDerived2<GenericValue>>(baseDerived2));
        }

        private sealed class GenericBaseWrapper
        {
            public int Id { get; set; }

            public List<GenericBase> GenericBases { get; private set; } = new List<GenericBase>();
        }

        private class GenericBase { }

        private sealed class GenericBaseDerived1<T> : GenericBase { }

        private sealed class GenericBaseDerived2<T> : GenericBase { }

        private sealed class GenericValue { }
    }
}
