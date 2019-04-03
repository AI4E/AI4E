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

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AI4E.TestHandlerConfiguration1("Assembly")]

namespace AI4E
{
    [TestClass]
    public class MessageHandlerActionDescriptorExtensionTests
    {
        [TestMethod]
        public void AssemblyConfigurationTest()
        {
            var descriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(NoConfigurationTestMessageHandler),
                typeof(NoConfigurationTestMessageHandler).GetMethod(nameof(NoConfigurationTestMessageHandler.NoConfigurationHandle)));

            var handlerConfig = descriptor.BuildConfiguration();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual("Assembly", ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).Value);
            Assert.AreEqual(typeof(string), ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.MessageType);
            Assert.AreEqual(typeof(NoConfigurationTestMessageHandler),
                ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.MessageHandlerType);
            Assert.AreEqual(typeof(NoConfigurationTestMessageHandler).GetMethod(nameof(NoConfigurationTestMessageHandler.NoConfigurationHandle)),
                ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.Member);
        }

        [TestMethod]
        public void TypeConfigurationTest()
        {
            var descriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(Configuration2TestMessageHandler),
                typeof(Configuration2TestMessageHandler).GetMethod(nameof(Configuration2TestMessageHandler.NoConfigurationHandle)));

            var handlerConfig = descriptor.BuildConfiguration();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual("Type", ((HandlerConfiguration2)data[typeof(HandlerConfiguration2)]).Value);
        }

        [TestMethod]
        public void MemberConfigurationTest()
        {
            var descriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(NoConfigurationTestMessageHandler),
                typeof(NoConfigurationTestMessageHandler).GetMethod(nameof(NoConfigurationTestMessageHandler.Configuration2Handle)));

            var handlerConfig = descriptor.BuildConfiguration();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual("Member", ((HandlerConfiguration2)data[typeof(HandlerConfiguration2)]).Value);
        }

        [TestMethod]
        public void MemberOverridesAssemblyConfigurationTest()
        {
            var descriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(NoConfigurationTestMessageHandler),
                typeof(NoConfigurationTestMessageHandler).GetMethod(nameof(NoConfigurationTestMessageHandler.Configuration1Handle)));

            var handlerConfig = descriptor.BuildConfiguration();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual("Member", ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).Value);
            Assert.AreEqual(typeof(string), ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.MessageType);
            Assert.AreEqual(typeof(NoConfigurationTestMessageHandler),
                ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.MessageHandlerType);
            Assert.AreEqual(typeof(NoConfigurationTestMessageHandler).GetMethod(nameof(NoConfigurationTestMessageHandler.Configuration1Handle)),
                ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.Member);
        }

        [TestMethod]
        public void TypeOverridesAssemblyConfigurationTest()
        {
            var descriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(Configuration1TestMessageHandler),
                typeof(Configuration1TestMessageHandler).GetMethod(nameof(Configuration1TestMessageHandler.NoConfigurationHandle)));

            var handlerConfig = descriptor.BuildConfiguration();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual("Type", ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).Value);
            Assert.AreEqual(typeof(string), ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.MessageType);
            Assert.AreEqual(typeof(Configuration1TestMessageHandler),
                ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.MessageHandlerType);
            Assert.AreEqual(typeof(Configuration1TestMessageHandler).GetMethod(nameof(NoConfigurationTestMessageHandler.NoConfigurationHandle)),
                ((HandlerConfiguration1)data[typeof(HandlerConfiguration1)]).MemberDescriptor.Member);
        }

        [TestMethod]
        public void MemberOverridesTypeConfigurationTest()
        {
            var descriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(Configuration2TestMessageHandler),
                typeof(Configuration2TestMessageHandler).GetMethod(nameof(Configuration2TestMessageHandler.Configuration2Handle)));

            var handlerConfig = descriptor.BuildConfiguration();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual("Member", ((HandlerConfiguration2)data[typeof(HandlerConfiguration2)]).Value);
        }
    }

    public class TestHandlerConfiguration1Attribute : ConfigureMessageHandlerAttribute
    {
        public TestHandlerConfiguration1Attribute(string value)
        {
            Value = value;
        }

        public string Value { get; }

        protected override void ConfigureMessageHandler(
            MessageHandlerActionDescriptor memberDescriptor,
            IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Configure<HandlerConfiguration1>(config =>
            {
                config.Value = Value;
                config.MemberDescriptor = memberDescriptor;
            });
        }
    }

    public class TestHandlerConfiguration2Attribute : ConfigureMessageHandlerAttribute
    {
        public TestHandlerConfiguration2Attribute(string value)
        {
            Value = value;
        }

        public string Value { get; }

        protected override void ConfigureMessageHandler(
            MessageHandlerActionDescriptor memberDescriptor,
            IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Configure<HandlerConfiguration2>(config => config.Value = Value);
        }
    }

    public class HandlerConfiguration1
    {
        public MessageHandlerActionDescriptor MemberDescriptor { get; set; }

        public string Value { get; set; }
    }

    public class HandlerConfiguration2
    {
        public string Value { get; set; }
    }

    public class NoConfigurationTestMessageHandler
    {
        public void NoConfigurationHandle(string message) { }

        [TestHandlerConfiguration1("Member")]
        public void Configuration1Handle(string message) { }

        [TestHandlerConfiguration2("Member")]
        public void Configuration2Handle(string message) { }
    }

    [TestHandlerConfiguration1("Type")]
    public class Configuration1TestMessageHandler
    {
        public void NoConfigurationHandle(string message) { }
    }

    [TestHandlerConfiguration2("Type")]
    public class Configuration2TestMessageHandler
    {
        public void NoConfigurationHandle(string message) { }

        [TestHandlerConfiguration2("Member")]
        public void Configuration2Handle(string message) { }
    }
}
