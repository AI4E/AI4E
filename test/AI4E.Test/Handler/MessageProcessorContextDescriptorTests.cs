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
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Handler
{
    [TestClass]
    public class MessageProcessorContextDescriptorTests
    {
        [TestMethod]
        public void DefaultDescriptorTest()
        {
            var descriptor = default(MessageProcessorContextDescriptor);
            var processor = new object();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(processor, null);
            });

            Assert.IsFalse(descriptor.CanSetContext);
        }

        [TestMethod]
        public void MessageProcessorPropertyWithAttributeTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessageProcessorPropertyWithoutAttribute));
            var processor = new MessageProcessorPropertyWithoutAttribute();
            var context = new MessageProcessorContextMock();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(processor, context);
            });

            Assert.IsFalse(descriptor.CanSetContext);
        }

        [TestMethod]
        public void MessagsProcessorPropertyInterfaceTypeTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessagsProcessorPropertyInterfaceType));
            var processor = new MessagsProcessorPropertyInterfaceType();
            var context = new MessageProcessorContextMock();

            descriptor.SetContext(processor, context);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, processor.Context);
        }

        [TestMethod]
        public void MessageProcessorPropertyObjectTypeTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessageProcessorPropertyObjectType));
            var processor = new MessageProcessorPropertyObjectType();
            var context = new MessageProcessorContextMock();

            descriptor.SetContext(processor, context);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, processor.Context);
        }

        [TestMethod]
        public void MessagsProcessorPropertySetOnlyTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessagsProcessorPropertySetOnly));
            var processor = new MessagsProcessorPropertySetOnly();
            var context = new MessageProcessorContextMock();

            descriptor.SetContext(processor, context);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, processor._context);
        }

        [TestMethod]
        public void MessagsProcessorPropertyNonPublicTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessagsProcessorPropertyNonPublic));
            var processor = new MessagsProcessorPropertyNonPublic();
            var context = new MessageProcessorContextMock();

            descriptor.SetContext(processor, context);

            Assert.IsTrue(descriptor.CanSetContext);
            Assert.AreSame(context, processor.Context);
        }

        [TestMethod]
        public void MessagsProcessorPropertyGetOnlyTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessagsProcessorPropertyGetOnly));
            var processor = new MessagsProcessorPropertyGetOnly();
            var context = new MessageProcessorContextMock();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(processor, context);
            });

            Assert.IsFalse(descriptor.CanSetContext);
        }

        [TestMethod]
        public void MessagsProcessorIndexerPropertyTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessagsProcessorIndexerProperty));
            var processor = new MessagsProcessorIndexerProperty();
            var context = new MessageProcessorContextMock();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                descriptor.SetContext(processor, context);
            });

            Assert.IsFalse(descriptor.CanSetContext);
        }

        [TestMethod]
        public void ProcessorTypeMismatchTest()
        {
            var descriptor = MessageProcessorContextDescriptor.GetDescriptor(typeof(MessagsProcessorPropertyNonPublic));
            var processor = new MessageProcessorPropertyObjectType();
            var context = new MessageProcessorContextMock();

            Assert.ThrowsException<ArgumentException>(() =>
            {
                descriptor.SetContext(processor, context);
            });
        }
    }

    public sealed class MessageProcessorContextMock : IMessageProcessorContext
    {
        public MessageHandlerConfiguration MessageHandlerConfiguration { get; set; }

        public MessageHandlerActionDescriptor MessageHandlerAction { get; set; }

        public object MessageHandler { get; set; }

        public bool IsPublish { get; set; }

        public bool IsLocalDispatch { get; set; }
    }

    public class MessageProcessorPropertyWithoutAttribute
    {
        public IMessageProcessorContext Context { get; set; }
    }

    public class MessagsProcessorPropertyInterfaceType
    {
        [MessageProcessorContext]
        public IMessageProcessorContext Context { get; set; }
    }

    public class MessageProcessorPropertyObjectType
    {
        [MessageProcessorContext]
        public object Context { get; set; }
    }

    public class MessagsProcessorPropertySetOnly
    {
        public IMessageProcessorContext _context;

        [MessageProcessorContext]
        public IMessageProcessorContext Context { set => _context = value; }
    }

    public class MessagsProcessorPropertyNonPublic
    {
        [MessageProcessorContext]
        internal IMessageProcessorContext Context { get; set; }
    }

    public class MessagsProcessorPropertyGetOnly
    {
        [MessageProcessorContext]
        public IMessageProcessorContext Context { get; } = null;
    }

    public class MessagsProcessorIndexerProperty
    {
        [MessageProcessorContext]
        [IndexerName("Context")]
        public IMessageProcessorContext this[int i]
        {
            get => throw null;
            set { }
        }
    }
}
