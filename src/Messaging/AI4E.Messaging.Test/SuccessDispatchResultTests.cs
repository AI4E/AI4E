/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

using System.Collections.Generic;
using AI4E.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class SuccessDispatchResultTests
    {
        [TestMethod]
        public void CreateMessageResultDataTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new SuccessDispatchResult("DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void CreateMessageTest()
        {
            var dispatchResult = new SuccessDispatchResult("DispatchResultMessage");

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
        }

        [TestMethod]
        public void CreateTest()
        {
            var dispatchResult = new SuccessDispatchResult();

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(SuccessDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
        }

        [TestMethod]
        public void GenericCreateMessageResultDataTest()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new SuccessDispatchResult<string>(result, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.AreSame(result, dispatchResult.Result);
        }

        [TestMethod]
        public void GenericCreateMessageTest()
        {
            var result = "thisistheresult";
            var dispatchResult = new SuccessDispatchResult<string>(result, "DispatchResultMessage");

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.AreSame(result, dispatchResult.Result);
        }

        [TestMethod]
        public void GenericCreateTest()
        {
            var result = "thisistheresult";
            var dispatchResult = new SuccessDispatchResult<string>(result);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(SuccessDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.AreSame(result, dispatchResult.Result);
        }

        [TestMethod]
        public void GenericToStringTest()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new SuccessDispatchResult<string>(result, "DispatchResultMessage", resultData);
            var desiredString = "Success: true - DispatchResultMessage[Result: thisistheresult]";

            Assert.AreEqual(desiredString, dispatchResult.ToString());
        }

        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new SuccessDispatchResult("DispatchResultMessage", resultData);
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsTrue(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void SerializeUnknownTypeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new SuccessDispatchResult("DispatchResultMessage", resultData);
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult);

            Assert.IsTrue(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void GenericSerializeRoundtripTest()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new SuccessDispatchResult<string>(result, "DispatchResultMessage", resultData);
            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsTrue(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.AreEqual(result, deserializedResult.Result);
        }

        [TestMethod]
        public void GenericSerializeUnknownTypeRoundtripTest()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new SuccessDispatchResult<string>(result, "DispatchResultMessage", resultData);
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult);

            Assert.IsTrue(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.AreEqual(result, deserializedResult.Result);
        }

        [TestMethod]
        public void FromResultTypeAndResultTest()
        {
            var result = "thisistheresult";
            var dispatchResult = SuccessDispatchResult.FromResult(typeof(object), result);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(SuccessDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<object>));
            Assert.AreSame(result, ((IDispatchResult<object>)dispatchResult).Result);
        }

        [TestMethod]
        public void FromResultTypeAndResult2Test()
        {
            var result = "thisistheresult";
            var dispatchResult = SuccessDispatchResult.FromResult(typeof(string), result);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(SuccessDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<string>));
            Assert.AreSame(result, ((IDispatchResult<string>)dispatchResult).Result);
        }

        [TestMethod]
        public void FromResultTypeResultMessageAndResultDataTest()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = SuccessDispatchResult.FromResult(typeof(object), result, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<object>));
            Assert.AreSame(result, ((IDispatchResult<object>)dispatchResult).Result);
        }

        [TestMethod]
        public void FromResultTypeResultMessageAndResultData2Test()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = SuccessDispatchResult.FromResult(typeof(string), result, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<string>));
            Assert.AreSame(result, ((IDispatchResult<string>)dispatchResult).Result);
        }

        [TestMethod]
        public void FromResultTest()
        {
            var result = "thisistheresult";
            var dispatchResult = SuccessDispatchResult.FromResult((object)result);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(SuccessDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<string>));
            Assert.AreSame(result, ((IDispatchResult<string>)dispatchResult).Result);
        }

        [TestMethod]
        public void FromResultMessageAndResultData2Test()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = SuccessDispatchResult.FromResult((object)result, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<string>));
            Assert.AreSame(result, ((IDispatchResult<string>)dispatchResult).Result);
        }

        [TestMethod]
        public void GenericFromResultTest()
        {
            var result = "thisistheresult";
            var dispatchResult = SuccessDispatchResult.FromResult<object>(result);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(SuccessDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<object>));
            Assert.AreSame(result, ((IDispatchResult<object>)dispatchResult).Result);
        }

        [TestMethod]
        public void GenericFromResult2Test()
        {
            var result = "thisistheresult";
            var dispatchResult = SuccessDispatchResult.FromResult(result);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(SuccessDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<string>));
            Assert.AreSame(result, ((IDispatchResult<string>)dispatchResult).Result);
        }

        [TestMethod]
        public void GenericFromResultMessageAndResultDataTest()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = SuccessDispatchResult.FromResult<object>(result, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<object>));
            Assert.AreSame(result, ((IDispatchResult<object>)dispatchResult).Result);
        }

        [TestMethod]
        public void GenericFromResultMessageAndResultData2Test()
        {
            var result = "thisistheresult";
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = SuccessDispatchResult.FromResult(result, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsInstanceOfType(dispatchResult, typeof(IDispatchResult<string>));
            Assert.AreSame(result, ((IDispatchResult<string>)dispatchResult).Result);
        }
    }
}
