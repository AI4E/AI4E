using System;
using System.Collections.Generic;
using AI4E.Messaging.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class StorageIssueDispatchResultTests : DispatchResultsTestsBase
    {
        [TestMethod]
        public void CreateMessageResultDataTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new StorageIssueDispatchResult("DispatchResultMessage", resultData);

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsNull(dispatchResult.Exception);
        }

        [TestMethod]
        public void CreateMessageTest()
        {
            var dispatchResult = new StorageIssueDispatchResult("DispatchResultMessage");

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsNull(dispatchResult.Exception);
        }

        [TestMethod]
        public void CreateTest()
        {
            var dispatchResult = new StorageIssueDispatchResult();

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual(StorageIssueDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsNull(dispatchResult.Exception);
        }


        [TestMethod]
        public void CreateExceptionTest()
        {
            var exception = new Exception("Exception message");
            var dispatchResult = new StorageIssueDispatchResult(exception);

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("An unhandled exception occured: Exception message", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.AreSame(exception, dispatchResult.Exception);
        }

        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new StorageIssueDispatchResult("DispatchResultMessage", resultData);
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void SerializeRoundtripExceptionTest()
        {
            var exception = new Exception("Exception message");

            var dispatchResult = new StorageIssueDispatchResult(exception);
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("An unhandled exception occured: Exception message", deserializedResult.Message);
            Assert.AreEqual(0, deserializedResult.ResultData.Count);
            Assert.IsInstanceOfType(deserializedResult.Exception, typeof(Exception));
            Assert.AreEqual("Exception message", deserializedResult.Exception.Message);
        }
    }
}
