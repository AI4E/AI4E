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
using System.Collections.Immutable;
using AI4E.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class DispatchResultTests
    {
        [TestMethod]
        public void CreateTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void ToStringTest()
        {
            var dispatchResult = new DispatchResult(false, "DispatchResultMessage");
            var desiredString = "Success: false - DispatchResultMessage";

            Assert.AreEqual(desiredString, dispatchResult.ToString());
        }

        [TestMethod]
        public void ToStringEmptyMessageTest()
        {
            var dispatchResult = new DispatchResult(false, "   ");
            var desiredString = "Success: false";

            Assert.AreEqual(desiredString, dispatchResult.ToString());
        }

        [TestMethod]
        public void CreateWithImmutableDataTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void DispatchDataUnknownKeyTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.IsNull(dispatchResult.ResultData["jkl"]);
        }

        [TestMethod]
        public void DispatchDataNullKeyTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.IsNull(dispatchResult.ResultData[null]);
        }

        [TestMethod]
        public void DispatchDataKeysCollectionTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.IsTrue(new HashSet<string>(new[] { "abc", "xyz" }).SetEquals(dispatchResult.ResultData.Keys));
        }

        [TestMethod]
        public void DispatchDataValuesCollectionTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.IsTrue(new HashSet<object>(new object[] { "def", 1234L }).SetEquals(dispatchResult.ResultData.Values));
        }

        [TestMethod]
        public void DispatchDataValuesContainsTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.ResultData.ContainsKey("abc"));
            Assert.IsTrue(dispatchResult.ResultData.ContainsKey("xyz"));
            Assert.IsFalse(dispatchResult.ResultData.ContainsKey("hjk"));
            Assert.IsFalse(dispatchResult.ResultData.ContainsKey("null"));
        }

        [TestMethod]
        public void DispatchDataValuesTryGetValueTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("abc", out var v));
            Assert.AreEqual("def", v);
            Assert.IsFalse(dispatchResult.ResultData.TryGetValue("hjk", out _));
            Assert.IsFalse(dispatchResult.ResultData.TryGetValue(null, out _));
        }

        [TestMethod]
        public void DispatchDataValuesGetEnumerableTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def"
            }.ToImmutableDictionary();

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);

            var enumerator = dispatchResult.ResultData.GetEnumerator();

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual(new KeyValuePair<string, object>("abc", "def"), enumerator.Current);
            Assert.IsFalse(enumerator.MoveNext());
            enumerator.Dispose();
        }

        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);
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

            var dispatchResult = new DispatchResult(true, "DispatchResultMessage", resultData);
            var deserializedResult = Serializer.RoundtripUnknownType(dispatchResult);

            Assert.IsTrue(deserializedResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.ResultData.Count);
            Assert.AreEqual("def", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
        }
    }
}
