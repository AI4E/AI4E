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
using System.Collections.Generic;
using AI4E.Messaging.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.MessageHandlers
{
    [TestClass]
    public class MessageHandlerContextDescriptorTests
    {
        [TestMethod]
        public void DefaultDescriptorTest()
        {
            var descriptor = MessageHandlerContextDescriptor.None;
            var handler = new object();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(handler, null);
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetDispatcher(handler, null);
            });

            Assert.IsFalse(descriptor.CanSetContext);
            Assert.IsFalse(descriptor.CanSetDispatcher);
        }

        [TestMethod]
        public void MessageHandlerPropertyWithoutAttributeTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessageHandlerPropertyWithoutAttribute));
            var handler = new MessageHandlerPropertyWithoutAttribute();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(handler, context);
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetDispatcher(handler, dispatcher);
            });

            Assert.IsFalse(descriptor.CanSetContext);
            Assert.IsFalse(descriptor.CanSetDispatcher);
        }

        [TestMethod]
        public void MessagsHandlerPropertyInterfaceTypeTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessagsHandlerPropertyInterfaceType));
            var handler = new MessagsHandlerPropertyInterfaceType();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            descriptor.SetContext(handler, context);
            descriptor.SetDispatcher(handler, dispatcher);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, handler.Context);
            Assert.IsTrue(descriptor.CanSetDispatcher);
            Assert.AreSame(dispatcher, handler.MessageDispatcher);
        }

        [TestMethod]
        public void MessageHandlerPropertyObjectTypeTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessageHandlerPropertyObjectType));
            var handler = new MessageHandlerPropertyObjectType();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            descriptor.SetContext(handler, context);
            descriptor.SetDispatcher(handler, dispatcher);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, handler.Context);
            Assert.IsTrue(descriptor.CanSetDispatcher);
            Assert.AreSame(dispatcher, handler.MessageDispatcher);
        }

        [TestMethod]
        public void MessagsHandlerPropertySetOnlyTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessagsHandlerPropertySetOnly));
            var handler = new MessagsHandlerPropertySetOnly();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            descriptor.SetContext(handler, context);
            descriptor.SetDispatcher(handler, dispatcher);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, handler._context);
            Assert.IsTrue(descriptor.CanSetDispatcher);
            Assert.AreSame(dispatcher, handler._dispatcher);
        }

        [TestMethod]
        public void MessagsHandlerPropertyNonPublicTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessagsHandlerPropertyNonPublic));
            var handler = new MessagsHandlerPropertyNonPublic();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            descriptor.SetContext(handler, context);
            descriptor.SetDispatcher(handler, dispatcher);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, handler.Context);
            Assert.IsTrue(descriptor.CanSetDispatcher);
            Assert.AreSame(dispatcher, handler.MessageDispatcher);
        }

        [TestMethod]
        public void MessagsHandlerPropertyGetOnlyTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessagsHandlerPropertyGetOnly));
            var handler = new MessagsHandlerPropertyGetOnly();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(handler, context);
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetDispatcher(handler, dispatcher);
            });

            Assert.IsFalse(descriptor.CanSetContext);
            Assert.IsFalse(descriptor.CanSetDispatcher);
        }

        [TestMethod]
        public void MessagsHandlerIndexerPropertyTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessagsHandlerIndexerProperty));
            var handler = new MessagsHandlerIndexerProperty();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(handler, context);
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetDispatcher(handler, dispatcher);
            });

            Assert.IsFalse(descriptor.CanSetContext);
            Assert.IsFalse(descriptor.CanSetDispatcher);
        }

        [TestMethod]
        public void HandlerTypeMismatchTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(MessagsHandlerPropertyNonPublic));
            var handler = new MessageHandlerPropertyObjectType();
            var context = new MessageDispatchContextMock();
            var dispatcher = new MessageDispatcherMock();

            Assert.ThrowsException<ArgumentException>(() =>
            {
                descriptor.SetContext(handler, context);
            });

            Assert.ThrowsException<ArgumentException>(() =>
            {
                descriptor.SetDispatcher(handler, dispatcher);
            });
        }

        [TestMethod]
        public void IsDefaultIfTypeIsDelegateTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(Func<object>));

            Assert.IsFalse(descriptor.CanSetContext);
            Assert.IsFalse(descriptor.CanSetDispatcher);
        }

        [TestMethod]
        public void IsDefaultIfTypeIsAbstractTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(AbstractMessagsHandler));

            Assert.IsFalse(descriptor.CanSetContext);
            Assert.IsFalse(descriptor.CanSetDispatcher);
        }

        [TestMethod]
        public void IsDefaultIfTypeIsEnumTest()
        {
            var descriptor = MessageHandlerContextDescriptor.GetDescriptor(typeof(ConsoleColor));

            Assert.IsFalse(descriptor.CanSetContext);
            Assert.IsFalse(descriptor.CanSetDispatcher);
        }

        [TestMethod]
        public void ThrowsIfTypeIsGenericTypeDefinitionTest()
        {
            Assert.ThrowsException<ArgumentException>(() =>
            {
                MessageHandlerContextDescriptor.GetDescriptor(typeof(GenericMessagsHandler<>));
            });
        }

        [TestMethod]
        public void ThrowsIfTypeIsGenericTypeDefinition2Test()
        {
            Assert.ThrowsException<ArgumentException>(() =>
            {
                MessageHandlerContextDescriptor.GetDescriptor(typeof(List<>));
            });
        }

        [TestMethod]
        public void ThrowsIfTypeIsNullTest()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                MessageHandlerContextDescriptor.GetDescriptor(null);
            });
        }
    }

    public class MessageHandlerPropertyWithoutAttribute
    {
        public IMessageDispatchContext Context { get; set; }

        public IMessageDispatcher MessageDispatcher { get; set; }
    }

    public class MessagsHandlerPropertyInterfaceType
    {
        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; set; }

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; set; }
    }

    public class MessageHandlerPropertyObjectType
    {
        [MessageDispatchContext]
        public object Context { get; set; }

        [MessageDispatcher]
        public object MessageDispatcher { get; set; }
    }

    public class MessagsHandlerPropertySetOnly
    {
        public IMessageDispatchContext _context;
        public IMessageDispatcher _dispatcher;

        [MessageDispatchContext]
        public IMessageDispatchContext Context { set => _context = value; }

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { set => _dispatcher = value; }
    }

    public class MessagsHandlerPropertyNonPublic
    {
        [MessageDispatchContext]
        internal IMessageDispatchContext Context { get; set; }

        [MessageDispatcher]
        internal IMessageDispatcher MessageDispatcher { get; set; }
    }

    public class MessagsHandlerPropertyGetOnly
    {
        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; } = null;

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; } = null;
    }

    public class MessagsHandlerIndexerProperty
    {
        [MessageDispatchContext]
        public IMessageDispatchContext this[int i]
        {
            get => throw null;
            set { }
        }

        [MessageDispatcher]
        public IMessageDispatcher this[string v]
        {
            get => throw null;
            set { }
        }
    }

    public abstract class AbstractMessagsHandler
    {
        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; set; }

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; set; }
    }

    public class GenericMessagsHandler<T>
    {
        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; set; }

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; set; }
    }
}
