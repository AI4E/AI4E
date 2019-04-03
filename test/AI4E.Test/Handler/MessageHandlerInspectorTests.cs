/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2019 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Handler
{
    [TestClass]
    public class MessageHandlerInspectorTests
    {
        [TestMethod]
        public void SyncHandlerTest()
        {
            var descriptor = MessageHandlerInspector.Instance.InspectType(typeof(SyncHandler)).Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(SyncHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(SyncHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void AsyncHandlerTest()
        {
            var descriptor = MessageHandlerInspector.Instance.InspectType(typeof(AsyncHandler)).Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(AsyncHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(AsyncHandler).GetMethod("HandleAsync"), descriptor.Member);
        }

        [TestMethod]
        public void SuffixSyncHandlerTest()
        {
            Assert.AreEqual(0, MessageHandlerInspector.Instance.InspectType(typeof(SuffixSyncHandler)).Count());
        }

        [TestMethod]
        public void MissingSuffixAsyncHandlerTest()
        {
            Assert.AreEqual(0, MessageHandlerInspector.Instance.InspectType(typeof(MissingSuffixAsyncHandler)).Count());
        }

        [TestMethod]
        public void WithRefParamHandlerTest()
        {
            Assert.AreEqual(0, MessageHandlerInspector.Instance.InspectType(typeof(WithRefParamHandler)).Count());
        }

        [TestMethod]
        public void GenericActionHandlerTest()
        {
            Assert.AreEqual(0, MessageHandlerInspector.Instance.InspectType(typeof(GenericActionHandler)).Count());
        }

        [TestMethod]
        public void EmptyParametersHandlerTest()
        {
            Assert.AreEqual(0, MessageHandlerInspector.Instance.InspectType(typeof(EmptyParametersHandler)).Count());
        }

        [TestMethod]
        public void NoActionAttributeHandlerTest()
        {
            Assert.AreEqual(0, MessageHandlerInspector.Instance.InspectType(typeof(NoActionAttributeHandler)).Count());
        }

        [TestMethod]
        public void SuffixSyncWithActionAttributeHandlerTest()
        {
            var descriptor = MessageHandlerInspector.Instance.InspectType(typeof(SuffixSyncWithActionAttributeHandler)).Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(SuffixSyncWithActionAttributeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(SuffixSyncWithActionAttributeHandler).GetMethod("HandleAsync"), descriptor.Member);
        }

        [TestMethod]
        public void MissingSuffixAsyncWithActionAttributeHandlerTest()
        {
            var descriptor = MessageHandlerInspector.Instance.InspectType(typeof(MissingSuffixAsyncWithActionAttributeHandler)).Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(MissingSuffixAsyncWithActionAttributeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(MissingSuffixAsyncWithActionAttributeHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void WithExplicitTypeHandlerTest()
        {
            var descriptor = MessageHandlerInspector.Instance.InspectType(typeof(WithExplicitTypeHandler)).Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(WithExplicitTypeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(WithExplicitTypeHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void WithExplicitClassTypeHandlerTest()
        {
            var descriptor = MessageHandlerInspector.Instance.InspectType(typeof(WithExplicitClassTypeHandler)).Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(WithExplicitClassTypeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(WithExplicitClassTypeHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void WithInvalidExplicitTypeHandlerTest()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                MessageHandlerInspector.Instance.InspectType(typeof(WithInvalidExplicitTypeHandler));
            });
        }

        [TestMethod]
        public void WithInvalidExplicitClassTypeHandlerTest()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                MessageHandlerInspector.Instance.InspectType(typeof(WithInvalidExplicitClassTypeHandler));
            });
        }
    }

    public sealed class SyncHandler
    {
        public int Handle(string x, int y)
        {
            throw null;
        }
    }

    public sealed class AsyncHandler
    {
        public Task<int> HandleAsync(string x, int y)
        {
            throw null;
        }
    }

    public sealed class SuffixSyncHandler
    {
        public int HandleAsync(string x, int y)
        {
            throw null;
        }
    }

    public sealed class MissingSuffixAsyncHandler
    {
        public Task<int> Handle(string x, int y)
        {
            throw null;
        }
    }

    public sealed class WithRefParamHandler
    {
        public int Handle(string x, ref int y)
        {
            throw null;
        }
    }

    public sealed class GenericActionHandler
    {
        public int Handle<T>(string x, int y)
        {
            throw null;
        }
    }

    public sealed class EmptyParametersHandler
    {
        public int Handle()
        {
            throw null;
        }
    }

    public sealed class NoActionAttributeHandler
    {
        [NoMessageHandler]
        public int Handle(string x, int y)
        {
            throw null;
        }
    }

    public sealed class SuffixSyncWithActionAttributeHandler
    {
        [MessageHandler]
        public int HandleAsync(string x, int y)
        {
            throw null;
        }
    }

    public sealed class MissingSuffixAsyncWithActionAttributeHandler
    {
        [MessageHandler]
        public Task<int> Handle(string x, int y)
        {
            throw null;
        }
    }

    public sealed class WithExplicitTypeHandler
    {
        [MessageHandler(typeof(string))]
        public int Handle(object x, int y)
        {
            throw null;
        }
    }

    [MessageHandler(typeof(string))]
    public sealed class WithExplicitClassTypeHandler
    {
        public int Handle(object x, int y)
        {
            throw null;
        }
    }

    public sealed class WithInvalidExplicitTypeHandler
    {
        [MessageHandler(typeof(WithInvalidExplicitTypeHandler))]
        public int Handle(string x, int y)
        {
            throw null;
        }
    }

    [MessageHandler(typeof(WithInvalidExplicitTypeHandler))]
    public sealed class WithInvalidExplicitClassTypeHandler
    {

        public int Handle(string x, int y)
        {
            throw null;
        }
    }
}
