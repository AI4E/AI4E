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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.MessageHandlers
{
    [TestClass]
    public sealed class MessageHandlerFeatureProviderTests
    {
        [TestMethod]
        public void PublicClassWithoutSuffixTest()
        {
            Assert.IsFalse(MessageHandlerResolver.IsMessageHandler(typeof(PublicClassWithoutSuffix)));
        }

        [TestMethod]
        public void PublicClassWithSuffixTest()
        {
            Assert.IsTrue(MessageHandlerResolver.IsMessageHandler(typeof(PublicClassWithSuffixHandler)));
        }

        [TestMethod]
        public void PublicClassWithAttributeTest()
        {
            Assert.IsTrue(MessageHandlerResolver.IsMessageHandler(typeof(PublicClassWithAttribute)));
        }

        [TestMethod]
        public void GenericPublicClassTest()
        {
            Assert.IsFalse(MessageHandlerResolver.IsMessageHandler(typeof(GenericPublicClassHandler<>)));
        }

        [TestMethod]
        public void AbstractPublicClassTest()
        {
            Assert.IsFalse(MessageHandlerResolver.IsMessageHandler(typeof(AbstractPublicClassHandler)));
        }

        [TestMethod]
        public void PublicClassWithNoHandlerAttributeTest()
        {
            Assert.IsFalse(MessageHandlerResolver.IsMessageHandler(typeof(PublicClassWithNoHandlerAttributeHandler)));
        }

        [TestMethod]
        public void InternalClassWithSuffixTest()
        {
            Assert.IsFalse(MessageHandlerResolver.IsMessageHandler(typeof(InternalClassWithSuffixHandler)));
        }

        [TestMethod]
        public void InternalClassWithAttributeTest()
        {
            Assert.IsTrue(MessageHandlerResolver.IsMessageHandler(typeof(InternalClassWithAttribute)));
        }

        private IEnumerable<Assembly> Assemblies
        {
            get
            {
                yield return typeof(PublicClassWithoutSuffix).Assembly;
                yield return typeof(PublicClassWithSuffixHandler).Assembly;
                yield return typeof(PublicClassWithAttribute).Assembly;
                yield return typeof(GenericPublicClassHandler<>).Assembly;
                yield return typeof(AbstractPublicClassHandler).Assembly;
                yield return typeof(PublicClassWithNoHandlerAttributeHandler).Assembly;
                yield return typeof(InternalClassWithSuffixHandler).Assembly;
                yield return typeof(InternalClassWithAttribute).Assembly;
                yield return typeof(PublicClassWithoutSuffix).Assembly;
                yield return typeof(PublicClassWithSuffixHandler).Assembly;
                yield return typeof(PublicClassWithAttribute).Assembly;
                yield return typeof(GenericPublicClassHandler<>).Assembly;
                yield return typeof(AbstractPublicClassHandler).Assembly;
                yield return typeof(PublicClassWithNoHandlerAttributeHandler).Assembly;
                yield return typeof(InternalClassWithSuffixHandler).Assembly;
                yield return typeof(InternalClassWithAttribute).Assembly;
            }
        }

        [TestMethod]
        public void PopulateFeaturesTest()
        {
            var assemblies = Assemblies;
            IMessageHandlerResolver provider = new MessageHandlerResolver();

            var messageHandlers = provider.ResolveMessageHandlers(assemblies);

            Assert.IsTrue(messageHandlers.Contains(typeof(PublicClassWithSuffixHandler)));
            Assert.IsTrue(messageHandlers.Contains(typeof(PublicClassWithAttribute)));
            Assert.IsTrue(messageHandlers.Contains(typeof(InternalClassWithAttribute)));

            Assert.IsFalse(messageHandlers.Contains(typeof(PublicClassWithoutSuffix)));
            Assert.IsFalse(messageHandlers.Contains(typeof(GenericPublicClassHandler<>)));
            Assert.IsFalse(messageHandlers.Contains(typeof(AbstractPublicClassHandler)));
            Assert.IsFalse(messageHandlers.Contains(typeof(PublicClassWithNoHandlerAttributeHandler)));
            Assert.IsFalse(messageHandlers.Contains(typeof(InternalClassWithSuffixHandler)));

        }
    }

    public class PublicClassWithoutSuffix { }

    public class PublicClassWithSuffixHandler { }

    [MessageHandler]
    public class PublicClassWithAttribute { }

    [MessageHandler]
    public class GenericPublicClassHandler<T> { }

    [MessageHandler]
    public abstract class AbstractPublicClassHandler { }

    [NoMessageHandler]
    [MessageHandler]
    public class PublicClassWithNoHandlerAttributeHandler { }

    internal class InternalClassWithSuffixHandler { }

    [MessageHandler]
    internal class InternalClassWithAttribute { }
}
