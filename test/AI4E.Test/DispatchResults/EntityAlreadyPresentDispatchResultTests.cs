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
using AI4E.Utils;
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
            Assert.IsNull(dispatchResult.EntityTypeName);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateMessageTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult("DispatchResultMessage");

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsNull(dispatchResult.EntityTypeName);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult();

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual(EntityAlreadyPresentDispatchResult.DefaultMessage, dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.IsNull(dispatchResult.EntityTypeName);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateEntityTypeTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string));

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the specified id is already present.", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), dispatchResult.EntityTypeName);
            Assert.IsTrue(dispatchResult.TryGetEntityType(out var entityTypeName));
            Assert.AreSame(typeof(string), entityTypeName);
            Assert.IsNull(dispatchResult.Id);
        }

        [TestMethod]
        public void CreateEntityTypeAndIdTest()
        {
            var dispatchResult = new EntityAlreadyPresentDispatchResult(typeof(string), "abc");

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.AreEqual($"An entity of type'{typeof(string)}' with the id 'abc' is already present.", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), dispatchResult.EntityTypeName);
            Assert.IsTrue(dispatchResult.TryGetEntityType(out var entityTypeName));
            Assert.AreSame(typeof(string), entityTypeName);
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
            Assert.IsNull(deserializedResult.EntityTypeName);
            Assert.IsFalse(deserializedResult.TryGetEntityType(out _));
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
            Assert.IsNull(deserializedResult.EntityTypeName);
            Assert.IsFalse(deserializedResult.TryGetEntityType(out _));
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
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), deserializedResult.EntityTypeName);
            Assert.IsTrue(deserializedResult.TryGetEntityType(out var entityTypeName));
            Assert.AreSame(typeof(string), entityTypeName);
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
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), deserializedResult.EntityTypeName);
            Assert.IsTrue(deserializedResult.TryGetEntityType(out var entityTypeName));
            Assert.AreSame(typeof(string), entityTypeName);
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
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), deserializedResult.EntityTypeName);
            Assert.IsTrue(deserializedResult.TryGetEntityType(out var entityTypeName));
            Assert.AreSame(typeof(string), entityTypeName);
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
            Assert.AreEqual(typeof(string).GetUnqualifiedTypeName(), deserializedResult.EntityTypeName);
            Assert.IsTrue(deserializedResult.TryGetEntityType(out var entityTypeName));
            Assert.AreSame(typeof(string), entityTypeName);
            Assert.AreEqual("abc", deserializedResult.Id);
        }
    }
}
