using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.DispatchResults
{
    [TestClass]
    public class EntityAlreadyPresentDispatchResultTests
    {
        [TestMethod]
        public void CreateMessageResultDataTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new EntityAlreadyPresentDispatchResult("DispatchResultMessage", resultData);

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsNull(dispatchResult.EntityType);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateMessageTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult("DispatchResultMessage");

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsNull(dispatchResult.EntityType);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult();

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual(EntityAlreadyPresentDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsNull(dispatchResult.EntityType);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateEntityTypeTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string));

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the specified id is already present.", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.AreSame(typeof(string), dispatchResult.EntityType);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateEntityTypeAndIdTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string), "abc");

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the id 'abc' is already present.", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.AreSame(typeof(string), dispatchResult.EntityType);
            Assert.AreEqual("abc", dispatchResult.Id);
        }

        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new EntityAlreadyPresentDispatchResult("DispatchResultMessage", resultData);
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.IsNull(deserializedResult.EntityType);
            Assert.IsNull(deserializedResult.Id);
        }

        [TestMethod]
        public void SerializeUnknownTypeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new EntityAlreadyPresentDispatchResult("DispatchResultMessage", resultData);
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.IsNull(deserializedResult.EntityType);
            Assert.IsNull(deserializedResult.Id);
        }

        [TestMethod]
        public void SerializeRoundtripEntityTypeTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string));
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the specified id is already present.", deserializedResult.Message);
            Assert.AreEqual(0, deserializedResult.ResultData.Count);
            Assert.AreSame(typeof(string), deserializedResult.EntityType);
            Assert.IsNull(deserializedResult.Id);
        }

        [TestMethod]
        public void SerializeUnknownTypeRoundtripEntityTypeTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string));
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the specified id is already present.", deserializedResult.Message);
            Assert.AreEqual(0, deserializedResult.ResultData.Count);
            Assert.AreSame(typeof(string), deserializedResult.EntityType);
            Assert.IsNull(deserializedResult.Id);
        }

        [TestMethod]
        public void SerializeRoundtripEntityTypeAndIdTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string), "abc");
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the id 'abc' is already present.", deserializedResult.Message);
            Assert.AreEqual(0, deserializedResult.ResultData.Count);
            Assert.AreSame(typeof(string), deserializedResult.EntityType);
            Assert.AreEqual("abc", deserializedResult.Id);
        }

        [TestMethod]
        public void SerializeUnknownTypeRoundtripEntityTypeAndIdTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string), "abc");
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the id 'abc' is already present.", deserializedResult.Message);
            Assert.AreEqual(0, deserializedResult.ResultData.Count);
            Assert.AreSame(typeof(string), deserializedResult.EntityType);
            Assert.AreEqual("abc", deserializedResult.Id);
        }
    }
}
