using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E
{
    [TestClass]
    public class MessageHandlerConfigurationTests
    {
        [TestMethod]
        public void DefaultTryGetConfigurationTest()
        {
            var handlerConfiguration = default(MessageHandlerConfiguration);

            Assert.IsFalse(handlerConfiguration.TryGetConfiguration<TestMessageHandlerConfiguration>(out var c2));
            Assert.IsNull(c2);
        }

        [TestMethod]
        public void DefaultGetConfigurationTest()
        {
            var handlerConfiguration = default(MessageHandlerConfiguration);
            var c1 = handlerConfiguration.GetConfiguration<TestMessageHandlerConfiguration>();

            Assert.IsNotNull(c1);
            Assert.AreEqual(0, c1.Int);
        }

        [TestMethod]
        public void DefaultIsEnabledTest()
        {
            var handlerConfiguration = default(MessageHandlerConfiguration);
            Assert.IsTrue(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(true));
            Assert.IsFalse(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(false));
        }

        [TestMethod]
        public void TryGetConfigurationNotPresentTest()
        {
            var data = new Dictionary<Type, object> { };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsFalse(handlerConfiguration.TryGetConfiguration<TestMessageHandlerConfiguration>(out var c1));
            Assert.IsNull(c1);
        }

        [TestMethod]
        public void TryGetConfigurationNullEntryTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = null
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsFalse(handlerConfiguration.TryGetConfiguration<TestMessageHandlerConfiguration>(out var c1));
            Assert.IsNull(c1);
        }

        [TestMethod]
        public void TryGetConfigurationWrongTypeEntryTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new object()
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsFalse(handlerConfiguration.TryGetConfiguration<TestMessageHandlerConfiguration>(out var c1));
            Assert.IsNull(c1);
        }

        [TestMethod]
        public void GetConfigurationNotPresentTest()
        {
            var data = new Dictionary<Type, object> { };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            var c1 = handlerConfiguration.GetConfiguration<TestMessageHandlerConfiguration>();

            Assert.IsNotNull(c1);
            Assert.AreEqual(0, c1.Int);
        }

        [TestMethod]
        public void GetConfigurationNullEntryTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = null
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            var c1 = handlerConfiguration.GetConfiguration<TestMessageHandlerConfiguration>();

            Assert.IsNotNull(c1);
            Assert.AreEqual(0, c1.Int);
        }

        [TestMethod]
        public void GetConfigurationWrongTypeEntryTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new object()
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            var c1 = handlerConfiguration.GetConfiguration<TestMessageHandlerConfiguration>();

            Assert.IsNotNull(c1);
            Assert.AreEqual(0, c1.Int);
        }

        [TestMethod]
        public void IsEnabledNotPresentTest()
        {
            var data = new Dictionary<Type, object> { };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsTrue(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(true));
            Assert.IsFalse(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(false));
        }

        [TestMethod]
        public void IsEnabledNullEntryTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfigurationFeature)] = null
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsTrue(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(true));
            Assert.IsFalse(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(false));
        }

        [TestMethod]
        public void IsEnabledWrongTypeEntryTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfigurationFeature)] = new object()
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsTrue(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(true));
            Assert.IsFalse(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(false));
        }

        [TestMethod]
        public void TryGetConfigurationTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 10 }
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsTrue(handlerConfiguration.TryGetConfiguration<TestMessageHandlerConfiguration>(out var c1));
            Assert.AreSame(data[typeof(TestMessageHandlerConfiguration)], c1);
        }

        [TestMethod]
        public void GetConfigurationTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 10 }
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            var c1 = handlerConfiguration.GetConfiguration<TestMessageHandlerConfiguration>();
            Assert.AreSame(data[typeof(TestMessageHandlerConfiguration)], c1);
        }

        [TestMethod]
        public void IsEnabledEnabledFeatureTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfigurationFeature)] = new TestMessageHandlerConfigurationFeature { IsEnabled = true }
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsTrue(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(false));
            Assert.IsTrue(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(true));
        }

        [TestMethod]
        public void IsEnabledDisabledFeatureTest()
        {
            var data = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfigurationFeature)] = new TestMessageHandlerConfigurationFeature { IsEnabled = false }
            };
            var handlerConfiguration = new MessageHandlerConfiguration(data.ToImmutableDictionary());

            Assert.IsFalse(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(false));
            Assert.IsFalse(handlerConfiguration.IsEnabled<TestMessageHandlerConfigurationFeature>(true));
        }
    }

    public class TestMessageHandlerConfiguration
    {
        public int Int { get; set; }
    }

    public class TestMessageHandlerConfigurationFeature : IMessageHandlerConfigurationFeature
    {
        public bool IsEnabled { get; set; }
    }
}
