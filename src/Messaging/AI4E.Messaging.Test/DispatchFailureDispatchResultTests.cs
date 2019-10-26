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

using System;
using System.Collections.Generic;
using AI4E.Internal;
using AI4E.Messaging.Test;
using AI4E.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class DispatchFailureDispatchResultTests
    {
        [TestMethod]
        public void CreateMessageTypeTest()
        {
            var dispatchResult = new DispatchFailureDispatchResult(typeof(string));
            var canLoadMessageType = dispatchResult.TryGetMessageType(out var messageType);

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual($"The message of type '{typeof(string)}' cannot be dispatched.", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsTrue(canLoadMessageType);
            Assert.AreSame(typeof(string), messageType);
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), dispatchResult.MessageTypeName);
        }

        [TestMethod]
        public void CreateMessageTypeAndMessageTest()
        {
            var dispatchResult = new DispatchFailureDispatchResult(typeof(string), "DispatchResultMessage");
            var canLoadMessageType = dispatchResult.TryGetMessageType(out var messageType);

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsTrue(canLoadMessageType);
            Assert.AreSame(typeof(string), messageType);
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), dispatchResult.MessageTypeName);
        }

        [TestMethod]
        public void CreateMessageTypeMessageAndResultDataTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };
            var dispatchResult = new DispatchFailureDispatchResult(typeof(string), "DispatchResultMessage", resultData);
            var canLoadMessageType = dispatchResult.TryGetMessageType(out var messageType);

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.IsTrue(canLoadMessageType);
            Assert.AreSame(typeof(string), messageType);
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), dispatchResult.MessageTypeName);
        }

        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new DispatchFailureDispatchResult(typeof(string), "DispatchResultMessage", resultData);
            var deserializedResult = Serializer.Roundtrip(dispatchResult);
            var canLoadMessageType = deserializedResult.TryGetMessageType(out var messageType);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.IsTrue(canLoadMessageType);
            Assert.AreSame(typeof(string), messageType);
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), deserializedResult.MessageTypeName);
        }

        [TestMethod]
        public void SerializeUnknownTypeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new DispatchFailureDispatchResult(typeof(string), "DispatchResultMessage", resultData);
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult);
            var canLoadMessageType = deserializedResult.TryGetMessageType(out var messageType);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.IsTrue(canLoadMessageType);
            Assert.AreSame(typeof(string), messageType);
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), deserializedResult.MessageTypeName);
        }

        [TestMethod]
        public void CustomTypeResolverSerializeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new DispatchFailureDispatchResult(typeof(CustomType), "DispatchResultMessage", resultData);
            var alc = new TestAssemblyLoadContext();
            var asm = alc.TestAssembly;
            var typeResolver = new TypeResolver(asm.Yield());
            var deserializedResult = Serializer.Roundtrip(dispatchResult, typeResolver);
            var canLoadMessageType = deserializedResult.TryGetMessageType(out var messageType);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.IsTrue(canLoadMessageType);
            Assert.AreSame(asm.GetType(typeof(CustomType).FullName), messageType);
            Assert.AreEqual(typeof(CustomType).GetUnqualifiedTypeName(), deserializedResult.MessageTypeName);
        }

        [TestMethod]
        public void CustomTypeResolverSerializeUnknownTypeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new DispatchFailureDispatchResult(typeof(CustomType), "DispatchResultMessage", resultData);
            var alc = new TestAssemblyLoadContext();
            var asm = alc.TestAssembly;
            var typeResolver = new TypeResolver(asm.Yield());
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult, typeResolver);
            var canLoadMessageType = deserializedResult.TryGetMessageType(out var messageType);

            Assert.IsFalse(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.IsTrue(canLoadMessageType);
            Assert.AreSame(asm.GetType(typeof(CustomType).FullName), messageType);
            Assert.AreEqual(typeof(CustomType).GetUnqualifiedTypeName(), deserializedResult.MessageTypeName);
        }
    }
}
