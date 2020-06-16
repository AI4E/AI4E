using System.Collections.Generic;
using AI4E.Messaging.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class NotFoundDispatchResultTests : DispatchResultsTestsBase
    {
        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new NotFoundDispatchResult("DispatchResultMessage", resultData);
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
        }
    }
}
