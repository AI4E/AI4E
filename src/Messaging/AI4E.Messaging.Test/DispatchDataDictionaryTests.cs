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
using System.Linq;
using AI4E.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class DispatchDataDictionaryTests
    {
        [TestMethod]
        public void GenericCreateMessageAndDataTest()
        {
            var message = "abcdef";
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<object>(message, data);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(object), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void GenericCreateMessageAndData2Test()
        {
            var message = "abcdef";
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>(message, data);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void GenericCreateMessageTest()
        {
            var message = "abcdef";
            var dispatchData = new DispatchDataDictionary<object>(message);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(object), dispatchData.MessageType);
            Assert.AreEqual(0, dispatchData.Count);
        }

        [TestMethod]
        public void GenericCreateMessage2Test()
        {
            var message = "abcdef";
            var dispatchData = new DispatchDataDictionary<string>(message);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(0, dispatchData.Count);
        }

        [TestMethod]
        public void CreateMessageTypeMessageAndDataTest()
        {
            var message = "abcdef";
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = DispatchDataDictionary.Create(typeof(object), message, data);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(object), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void CreateMessageTypeMessageAndData2Test()
        {
            var message = "abcdef";
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = DispatchDataDictionary.Create(typeof(string), message, data);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void CreateMessageTypeMessageTest()
        {
            var message = "abcdef";
            var dispatchData = DispatchDataDictionary.Create(typeof(object), message);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(object), dispatchData.MessageType);
            Assert.AreEqual(0, dispatchData.Count);
        }

        [TestMethod]
        public void CreateMessageTypeMessage2Test()
        {
            var message = "abcdef";
            var dispatchData = DispatchDataDictionary.Create(typeof(string), message);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(0, dispatchData.Count);
        }

        [TestMethod]
        public void CreateMessageAndDataTest()
        {
            var message = "abcdef";
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = DispatchDataDictionary.Create(message, data);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void CreateMessageTest()
        {
            var message = "abcdef";
            var dispatchData = DispatchDataDictionary.Create(message);
            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(0, dispatchData.Count);
        }

        [TestMethod]
        public void UnknownKeyTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);

            Assert.IsNull(dispatchData["x"]);
        }

        [TestMethod]
        public void NullKeyTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);

            Assert.IsNull(dispatchData[null]);
        }

        [TestMethod]
        public void KeysCollectionTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);

            Assert.IsTrue(new HashSet<string>(new[] { "a", "b", "c" }).SetEquals(dispatchData.Keys));
        }

        [TestMethod]
        public void ValuesCollectionTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);

            Assert.IsTrue(new HashSet<object>(new object[] { "xyz", 123L, ConsoleColor.Black }).SetEquals(dispatchData.Values));
        }

        [TestMethod]
        public void ContainsTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);

            Assert.IsTrue(dispatchData.ContainsKey("a"));
            Assert.IsTrue(dispatchData.ContainsKey("b"));
            Assert.IsTrue(dispatchData.ContainsKey("c"));
            Assert.IsFalse(dispatchData.ContainsKey("x"));
            Assert.IsFalse(dispatchData.ContainsKey(null));
        }

        [TestMethod]
        public void TryGetValueTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);

            Assert.IsTrue(dispatchData.TryGetValue("a", out var value));
            Assert.AreEqual("xyz", value);
            Assert.IsTrue(dispatchData.TryGetValue("b", out value));
            Assert.AreEqual(123L, value);
            Assert.IsTrue(dispatchData.TryGetValue("c", out value));
            Assert.AreEqual(ConsoleColor.Black, value);
            Assert.IsFalse(dispatchData.TryGetValue("x", out _));
            Assert.IsFalse(dispatchData.TryGetValue(null, out _));
        }

        [TestMethod]
        public void GetEnumerableTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);
            var values = dispatchData.ToList();

            Assert.AreEqual(3, values.Count);

            Assert.IsTrue(values.ToHashSet().SetEquals(data));
        }

        [TestMethod]
        public void EnumerateTest()
        {
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };

            var dispatchData = new DispatchDataDictionary<string>("abcdef", data);
            var data2 = new Dictionary<string, object>();

            foreach (var kvp in dispatchData)
            {
                data2.Add(kvp.Key, kvp.Value);
            }

            Assert.AreEqual(data.Count, data2.Count);
            Assert.IsTrue(data.ToHashSet().SetEquals(data2));
        }

        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var message = "abcdef";
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<object>(message, data);
            var deserializedData = Serializer.Roundtrip(dispatchData);

            Assert.AreEqual(message, deserializedData.Message);
            Assert.AreSame(typeof(object), deserializedData.MessageType);
            Assert.AreEqual(3, deserializedData.Count);
            Assert.AreEqual("xyz", deserializedData["a"]);
            Assert.AreEqual(123L, deserializedData["b"]);
            Assert.AreEqual(ConsoleColor.Black, deserializedData["c"]);
        }

        //[TestMethod]
        public void SerializeUnknownTypeRoundtripTest()
        {
            var message = "abcdef";
            var data = new Dictionary<string, object>
            {
                ["a"] = "xyz",
                ["b"] = 123L,
                ["c"] = ConsoleColor.Black
            };
            var dispatchData = new DispatchDataDictionary<object>(message, data);
            var deserializedData = Serializer.RoundtripUnknownType(dispatchData);

            Assert.AreEqual(message, deserializedData.Message);
            Assert.AreSame(typeof(object), deserializedData.MessageType);
            Assert.AreEqual(3, deserializedData.Count);
            Assert.AreEqual("xyz", deserializedData["a"]);
            Assert.AreEqual(123L, deserializedData["b"]);
            Assert.AreEqual(ConsoleColor.Black, deserializedData["c"]);
        }
    }
}
