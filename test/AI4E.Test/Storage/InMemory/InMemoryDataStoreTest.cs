using System;
using System.Threading.Tasks;
using AI4E.Storage;
using AI4E.Storage.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Storage.InMemory
{
    [TestClass]
    public class InMemoryDataStoreTest
    {
        [TestMethod]
        public async Task BasicTest()
        {
            var store = new InMemoryDatabase();

            var model = new DataModel { Id = Guid.NewGuid(), X = 1, Y = 2 };

            await store.AddAsync(model);

            Assert.AreEqual(1, model.X);
            Assert.AreEqual(2, model.Y);

            var model2 = await store.GetOneAsync<DataModel>();

            Assert.IsNotNull(model2);
            Assert.AreEqual(1, model.X);
            Assert.AreEqual(2, model.Y);
            Assert.AreEqual(1, model2.X);
            Assert.AreEqual(2, model2.Y);

            model2.X = 1000;

            Assert.AreEqual(1, model.X);

            await store.UpdateAsync(model2);

            Assert.AreEqual(1, model.X);
            Assert.AreEqual(2, model.Y);
            Assert.AreEqual(1000, model2.X);
            Assert.AreEqual(2, model2.Y);
        }

        [TestMethod]
        public async Task NoIdPropertyTest()
        {
            var store = new InMemoryDatabase();

            var model = new InvalidDataModel { };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store.AddAsync(model).AsTask());
        }

        [TestMethod]
        public async Task WronglyTypedIdPropertyTest()
        {
            var store = new InMemoryDatabase();

            var model = new InvalidDataModel2 { };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store.AddAsync(model).AsTask());
        }
    }

    public class DataModel
    {
        public Guid Id { get; set; }

        public float X { get; set; }

        public float Y { get; set; }
    }

    public class InvalidDataModel
    {

    }

    public class InvalidDataModel2
    {
        public string Id { get; set; }
    }
}
