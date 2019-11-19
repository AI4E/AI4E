using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public sealed class DispatchDataDictionaryBuilderTests
    {
        [TestMethod]
        public void GenericCreateMessageAndDataTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder<object>(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            var dispatchData = builder.BuildDispatchDataDictionary();

            Assert.AreSame(message, builder.Message);
            Assert.AreSame(typeof(object), builder.MessageType);

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
            var builder = DispatchDataDictionary.CreateBuilder(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            var dispatchData = builder.BuildDispatchDataDictionary();

            Assert.AreSame(message, builder.Message);
            Assert.AreSame(typeof(string), builder.MessageType);

            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void CreateMessageTypeMessageAndDataTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(typeof(object), message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            var dispatchData = builder.BuildDispatchDataDictionary();

            Assert.AreSame(message, builder.Message);
            Assert.AreSame(typeof(object), builder.MessageType);

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
            var builder = DispatchDataDictionary.CreateBuilder(typeof(string), message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            var dispatchData = builder.BuildDispatchDataDictionary();

            Assert.AreSame(message, builder.Message);
            Assert.AreSame(typeof(string), builder.MessageType);

            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void CreateMessageAndDataTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder((object)message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            var dispatchData = builder.BuildDispatchDataDictionary();

            Assert.AreSame(message, builder.Message);
            Assert.AreSame(typeof(string), builder.MessageType);

            Assert.AreSame(message, dispatchData.Message);
            Assert.AreSame(typeof(string), dispatchData.MessageType);
            Assert.AreEqual(3, dispatchData.Count);
            Assert.AreEqual("xyz", dispatchData["a"]);
            Assert.AreEqual(123L, dispatchData["b"]);
            Assert.AreEqual(ConsoleColor.Black, dispatchData["c"]);
        }

        [TestMethod]
        public void UnknownKeyTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            Assert.IsNull(builder["x"]);
        }

        [TestMethod]
        public void NullKeyTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            Assert.IsNull(builder[null]);
        }

        [TestMethod]
        public void KeysCollectionTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            Assert.IsTrue(new HashSet<string>(new[] { "a", "b", "c" }).SetEquals(builder.Keys));
        }

        [TestMethod]
        public void ValuesCollectionTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            Assert.IsTrue(new HashSet<object>(new object[] { "xyz", 123L, ConsoleColor.Black }).SetEquals(builder.Values));
        }

        [TestMethod]
        public void ContainsTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            Assert.IsTrue(builder.ContainsKey("a"));
            Assert.IsTrue(builder.ContainsKey("b"));
            Assert.IsTrue(builder.ContainsKey("c"));
            Assert.IsFalse(builder.ContainsKey("x"));
            Assert.IsFalse(builder.ContainsKey(null));
        }

        [TestMethod]
        public void TryGetValueTest()
        {
            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);
            builder["a"] = "xyz";
            builder["b"] = 123L;
            builder["c"] = ConsoleColor.Black;

            Assert.IsTrue(builder.TryGetValue("a", out var value));
            Assert.AreEqual("xyz", value);
            Assert.IsTrue(builder.TryGetValue("b", out value));
            Assert.AreEqual(123L, value);
            Assert.IsTrue(builder.TryGetValue("c", out value));
            Assert.AreEqual(ConsoleColor.Black, value);
            Assert.IsFalse(builder.TryGetValue("x", out _));
            Assert.IsFalse(builder.TryGetValue(null, out _));
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

            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);

            foreach (var kvp in data)
                builder.Add(kvp);

            var values = builder.ToList();

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

            var message = "abcdef";
            var builder = DispatchDataDictionary.CreateBuilder(message);
            foreach (var kvp in data)
                builder.Add(kvp);

            var data2 = new Dictionary<string, object>();

            foreach (var kvp in builder)
            {
                data2.Add(kvp.Key, kvp.Value);
            }

            Assert.AreEqual(data.Count, data2.Count);
            Assert.IsTrue(data.ToHashSet().SetEquals(data2));
        }
    }
}
