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
using AI4E.Utils.ApplicationParts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.Handler
{
    [TestClass]
    public sealed class MessageHandlerFeatureProviderTests
    {
        [TestMethod]
        public void PublicClassWithoutSuffixTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsFalse(provider.IsMessageHandler(typeof(PublicClassWithoutSuffix)));
        }

        [TestMethod]
        public void PublicClassWithSuffixTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsTrue(provider.IsMessageHandler(typeof(PublicClassWithSuffixHandler)));
        }

        [TestMethod]
        public void PublicClassWithAttributeTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsTrue(provider.IsMessageHandler(typeof(PublicClassWithAttribute)));
        }

        [TestMethod]
        public void GenericPublicClassTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsFalse(provider.IsMessageHandler(typeof(GenericPublicClassHandler<>)));
        }

        [TestMethod]
        public void AbstractPublicClassTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsFalse(provider.IsMessageHandler(typeof(AbstractPublicClassHandler)));
        }

        [TestMethod]
        public void PublicClassWithNoHandlerAttributeTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsFalse(provider.IsMessageHandler(typeof(PublicClassWithNoHandlerAttributeHandler)));
        }

        [TestMethod]
        public void InternalClassWithSuffixTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsFalse(provider.IsMessageHandler(typeof(InternalClassWithSuffixHandler)));
        }

        [TestMethod]
        public void InternalClassWithAttributeTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsTrue(provider.IsMessageHandler(typeof(InternalClassWithAttribute)));
        }

        [TestMethod]
        public void PopulateFeaturesTest()
        {
            var appParts = new[]
            {
                // We register this twice to test type deduplication
                new ApplicationPartTypeProviderMock(),
                new ApplicationPartTypeProviderMock()
            };

            var feature = new MessageHandlerFeature();
            var provider = new MessageHandlerFeatureProvider();

            provider.PopulateFeature(appParts, feature);

            Assert.AreEqual(3, feature.MessageHandlers.Count);
            Assert.IsTrue(new[]
            {
                typeof(PublicClassWithSuffixHandler),
                typeof(PublicClassWithAttribute),
                typeof(InternalClassWithAttribute)
            }.SequenceEqual(feature.MessageHandlers));
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

    public sealed class ApplicationPartTypeProviderMock : ApplicationPart, IApplicationPartTypeProvider
    {
        public IEnumerable<TypeInfo> Types
        {
            get
            {
                yield return typeof(PublicClassWithoutSuffix).GetTypeInfo();
                yield return typeof(PublicClassWithSuffixHandler).GetTypeInfo();
                yield return typeof(PublicClassWithAttribute).GetTypeInfo();
                yield return typeof(GenericPublicClassHandler<>).GetTypeInfo();
                yield return typeof(AbstractPublicClassHandler).GetTypeInfo();
                yield return typeof(PublicClassWithNoHandlerAttributeHandler).GetTypeInfo();
                yield return typeof(InternalClassWithSuffixHandler).GetTypeInfo();
                yield return typeof(InternalClassWithAttribute).GetTypeInfo();
            }
        }

        public override string Name => nameof(ApplicationPartTypeProviderMock);
    }
}
