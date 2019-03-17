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
            var inspector = new MessageHandlerInspector(typeof(SyncHandler));

            var descriptor = inspector.GetHandlerDescriptors().Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(SyncHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(SyncHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void AsyncHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(AsyncHandler));

            var descriptor = inspector.GetHandlerDescriptors().Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(AsyncHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(AsyncHandler).GetMethod("HandleAsync"), descriptor.Member);
        }

        [TestMethod]
        public void SuffixSyncHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(SuffixSyncHandler));

            Assert.AreEqual(0, inspector.GetHandlerDescriptors().Count());
        }

        [TestMethod]
        public void MissingSuffixAsyncHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(MissingSuffixAsyncHandler));

            Assert.AreEqual(0, inspector.GetHandlerDescriptors().Count());
        }

        [TestMethod]
        public void WithRefParamHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(WithRefParamHandler));

            Assert.AreEqual(0, inspector.GetHandlerDescriptors().Count());
        }

        [TestMethod]
        public void GenericActionHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(GenericActionHandler));

            Assert.AreEqual(0, inspector.GetHandlerDescriptors().Count());
        }

        [TestMethod]
        public void EmptyParametersHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(EmptyParametersHandler));

            Assert.AreEqual(0, inspector.GetHandlerDescriptors().Count());
        }

        [TestMethod]
        public void NoActionAttributeHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(NoActionAttributeHandler));

            Assert.AreEqual(0, inspector.GetHandlerDescriptors().Count());
        }

        [TestMethod]
        public void SuffixSyncWithActionAttributeHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(SuffixSyncWithActionAttributeHandler));

            var descriptor = inspector.GetHandlerDescriptors().Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(SuffixSyncWithActionAttributeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(SuffixSyncWithActionAttributeHandler).GetMethod("HandleAsync"), descriptor.Member);
        }

        [TestMethod]
        public void MissingSuffixAsyncWithActionAttributeHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(MissingSuffixAsyncWithActionAttributeHandler));

            var descriptor = inspector.GetHandlerDescriptors().Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(MissingSuffixAsyncWithActionAttributeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(MissingSuffixAsyncWithActionAttributeHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void WithExplicitTypeHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(WithExplicitTypeHandler));

            var descriptor = inspector.GetHandlerDescriptors().Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(WithExplicitTypeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(WithExplicitTypeHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void WithExplicitClassTypeHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(WithExplicitClassTypeHandler));

            var descriptor = inspector.GetHandlerDescriptors().Single();

            Assert.AreEqual(typeof(string), descriptor.MessageType);
            Assert.AreEqual(typeof(WithExplicitClassTypeHandler), descriptor.MessageHandlerType);
            Assert.AreEqual(typeof(WithExplicitClassTypeHandler).GetMethod("Handle"), descriptor.Member);
        }

        [TestMethod]
        public void WithInvalidExplicitTypeHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(WithInvalidExplicitTypeHandler));

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                inspector.GetHandlerDescriptors();
            });
        }

        [TestMethod]
        public void WithInvalidExplicitClassTypeHandlerTest()
        {
            var inspector = new MessageHandlerInspector(typeof(WithInvalidExplicitClassTypeHandler));

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                inspector.GetHandlerDescriptors();
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
