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
using AI4E.Messaging.Serialization;
using AI4E.Messaging.Test;
using AI4E.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{

    [TestClass]
    public class AggregateDispatchResultTests : DispatchResultsTestsBase
    {
        [TestMethod]
        public void EmptyAggregateDispatchResultTest()
        {
            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[0]);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("{ No results }", dispatchResult.Message);
            Assert.AreEqual(0, dispatchResult.DispatchResults.Count());
            Assert.AreEqual(0, dispatchResult.ResultData.Count);
        }

        [TestMethod]
        public void ResultDataOnlytest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[0], resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("{ No results }", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void SingleDispatchResultTest()
        {
            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData);

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, null
            });

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("DispatchResultMessage", dispatchResult.Message);
            Assert.AreEqual(1, dispatchResult.DispatchResults.Count());
            Assert.AreSame(d1, dispatchResult.DispatchResults.First());
            Assert.AreEqual(2, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
        }

        [TestMethod]
        public void MultipleDispatchResultTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            });

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("{ Multiple results }", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.DispatchResults.Count());
            Assert.AreSame(d1, dispatchResult.DispatchResults.First());
            Assert.AreSame(d2, dispatchResult.DispatchResults.Last());
            Assert.AreEqual(4, dispatchResult.ResultData.Count);
            Assert.AreEqual("def", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.AreEqual("u", dispatchResult.ResultData["z"]);
            Assert.AreEqual(167L, dispatchResult.ResultData["b"]);
        }

        [TestMethod]
        public void OverrideResultDataTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "j",
                ["b"] = null,
                ["c"] = 5L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            }, resultData);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual("{ Multiple results }", dispatchResult.Message);
            Assert.AreEqual(2, dispatchResult.DispatchResults.Count());
            Assert.AreSame(d1, dispatchResult.DispatchResults.First());
            Assert.AreSame(d2, dispatchResult.DispatchResults.Last());
            Assert.AreEqual(4, dispatchResult.ResultData.Count);
            Assert.AreEqual("j", dispatchResult.ResultData["abc"]);
            Assert.AreEqual(1234L, dispatchResult.ResultData["xyz"]);
            Assert.AreEqual("u", dispatchResult.ResultData["z"]);
            Assert.AreEqual(5L, dispatchResult.ResultData["c"]);
            Assert.IsFalse(dispatchResult.ResultData.ContainsKey("b"));
        }

        [TestMethod]
        public void ResultDataTryGetValueTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "j",
                ["b"] = null,
                ["c"] = 5L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            }, resultData);

            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("abc", out var value));
            Assert.AreEqual("j", value);
            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("xyz", out value));
            Assert.AreEqual(1234L, value);
            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("z", out value));
            Assert.AreEqual("u", value);
            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("c", out value));
            Assert.AreEqual(5L, value);
            Assert.IsFalse(dispatchResult.ResultData.TryGetValue("b", out _));
            Assert.IsFalse(dispatchResult.ResultData.TryGetValue(null, out _));

            // This triggers internal combination of values
            Assert.AreEqual(4, dispatchResult.ResultData.Count);

            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("abc", out value));
            Assert.AreEqual("j", value);
            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("xyz", out value));
            Assert.AreEqual(1234L, value);
            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("z", out value));
            Assert.AreEqual("u", value);
            Assert.IsTrue(dispatchResult.ResultData.TryGetValue("c", out value));
            Assert.AreEqual(5L, value);
            Assert.IsFalse(dispatchResult.ResultData.TryGetValue("b", out _));
            Assert.IsFalse(dispatchResult.ResultData.TryGetValue(null, out _));
        }

        [TestMethod]
        public void ResultDataKeysTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "j",
                ["b"] = null,
                ["c"] = 5L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            }, resultData);

            Assert.IsTrue(new HashSet<string>(new[] { "abc", "xyz", "z", "c" }).SetEquals(dispatchResult.ResultData.Keys));
        }

        [TestMethod]
        public void ResultDataValuesTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "j",
                ["b"] = null,
                ["c"] = 5L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            }, resultData);

            Assert.IsTrue(new HashSet<object>(new object[] { "j", 5L, "u", 1234L }).SetEquals(dispatchResult.ResultData.Values));
        }

        [TestMethod]
        public void ResultDataGetEnumeratorTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "j",
                ["b"] = null,
                ["c"] = 5L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            }, resultData);

            var list = dispatchResult.ResultData.ToList();
            var comparand = new HashSet<KeyValuePair<string, object>>(
                new KeyValuePair<string, object>[]
                {
                    new KeyValuePair<string, object>("abc", "j"),
                    new KeyValuePair<string, object>("c", 5L),
                    new KeyValuePair<string, object>("z", "u"),
                    new KeyValuePair<string, object>("xyz", 1234L),
                });

            Assert.IsTrue(comparand.SetEquals(list));
        }

        [TestMethod]
        public void SerializeRoundtripTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = "def",
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var resultData = new Dictionary<string, object>
            {
                ["abc"] = "j",
                ["b"] = null,
                ["c"] = 5L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            }, resultData);

            var deserializedResult = Serializer.Roundtrip(dispatchResult);

            Assert.IsTrue(deserializedResult.IsSuccess);
            Assert.AreEqual("{ Multiple results }", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.DispatchResults.Count());
            Assert.AreEqual(4, deserializedResult.ResultData.Count);
            Assert.AreEqual("j", deserializedResult.ResultData["abc"]);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.AreEqual("u", deserializedResult.ResultData["z"]);
            Assert.AreEqual(5L, deserializedResult.ResultData["c"]);
            Assert.IsFalse(deserializedResult.ResultData.ContainsKey("b"));
        }

        [TestMethod]
        public void CustomTypeResolverSerializeRoundtripTest()
        {
            var resultData1 = new Dictionary<string, object>
            {
                ["abc"] = new CustomType { Str = "def" },
                ["xyz"] = 1234L
            };

            var resultData2 = new Dictionary<string, object>
            {
                ["z"] = "u",
                ["b"] = 167L
            };

            var d1 = new DispatchResult(true, "DispatchResultMessage", resultData1);
            var d2 = new DispatchResult(true, "yb", resultData2);

            var resultData = new Dictionary<string, object>
            {
                ["abc"] = new CustomType { Str = "j" },
                ["b"] = null,
                ["c"] = 5L
            };

            var dispatchResult = new AggregateDispatchResult(new IDispatchResult[]
            {
                d1, d2, null
            }, resultData);

            var alc = new TestAssemblyLoadContext();
            var asm = alc.TestAssembly;
            var typeResolver = new TypeResolver(asm.Yield());

            var deserializedResult = BuildSerializer(typeResolver).Roundtrip(dispatchResult);

            Assert.IsTrue(deserializedResult.IsSuccess);
            Assert.AreEqual("{ Multiple results }", deserializedResult.Message);
            Assert.AreEqual(2, deserializedResult.DispatchResults.Count());
            Assert.AreEqual(4, deserializedResult.ResultData.Count);
            Assert.IsInstanceOfType(deserializedResult.ResultData["abc"], asm.GetType(typeof(CustomType).FullName));
            Assert.AreEqual("j", (string)((dynamic)deserializedResult.ResultData["abc"]).Str);
            Assert.AreEqual(1234L, deserializedResult.ResultData["xyz"]);
            Assert.AreEqual("u", deserializedResult.ResultData["z"]);
            Assert.AreEqual(5L, deserializedResult.ResultData["c"]);
            Assert.IsFalse(deserializedResult.ResultData.ContainsKey("b"));
        }
    }
}
