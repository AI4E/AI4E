using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E
{
    [TestClass]
    public class MessageHandlerConfigurationBuilderTests
    {
        [TestMethod]
        public void EmptyBuilderTest()
        {
            var builder = new MessageHandlerConfigurationBuilder();
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.Count);
        }

        [TestMethod]
        public void ConfigureFunc2DoNothingTest()
        {
            var builder = new MessageHandlerConfigurationBuilder();

            builder.Configure<TestMessageHandlerConfiguration>(config =>
            {
                Assert.IsNull(config);
                return config;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.Count);
        }

        [TestMethod]
        public void ConfigureFunc2AddTest()
        {
            var builder = new MessageHandlerConfigurationBuilder();
            var desired = new TestMessageHandlerConfiguration { Int = 30 };

            builder.Configure<TestMessageHandlerConfiguration>(config =>
            {
                Assert.IsNull(config);
                return desired;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
        }

        [TestMethod]
        public void ConfigureFunc1AddTest()
        {
            var builder = new MessageHandlerConfigurationBuilder();
            var desired = new TestMessageHandlerConfiguration { Int = 30 };

            builder.Configure<TestMessageHandlerConfiguration>(() =>
            {
                return desired;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
        }

        [TestMethod]
        public void ConfigureActionAddTest()
        {
            var builder = new MessageHandlerConfigurationBuilder();

            builder.Configure<TestMessageHandlerConfiguration>(config =>
            {
                config.Int = 20;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual(20, ((TestMessageHandlerConfiguration)data[typeof(TestMessageHandlerConfiguration)]).Int);
        }

        [TestMethod]
        public void ConfigureFunc2UpdateTest()
        {
            var existing = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 20 }
            };
            var builder = new MessageHandlerConfigurationBuilder(existing);

            builder.Configure<TestMessageHandlerConfiguration>(config =>
            {
                config.Int = 30;
                return config;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual(30, ((TestMessageHandlerConfiguration)data[typeof(TestMessageHandlerConfiguration)]).Int);
        }

        [TestMethod]
        public void ConfigureActionUpdateTest()
        {
            var existing = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 20 }
            };
            var builder = new MessageHandlerConfigurationBuilder(existing);

            builder.Configure<TestMessageHandlerConfiguration>(config =>
            {
                config.Int = 30;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreEqual(30, ((TestMessageHandlerConfiguration)data[typeof(TestMessageHandlerConfiguration)]).Int);
        }

        [TestMethod]
        public void ConfigureFunc2ReplaceTest()
        {
            var existing = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 20 }
            };
            var builder = new MessageHandlerConfigurationBuilder(existing);
            var desired = new TestMessageHandlerConfiguration { Int = 30 };

            builder.Configure<TestMessageHandlerConfiguration>(config =>
            {
                Assert.IsNotNull(config);
                return desired;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreSame(desired, data[typeof(TestMessageHandlerConfiguration)]);
        }

        [TestMethod]
        public void ConfigureFunc1ReplaceTest()
        {
            var existing = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 20 }
            };
            var builder = new MessageHandlerConfigurationBuilder(existing);
            var desired = new TestMessageHandlerConfiguration { Int = 30 };

            builder.Configure<TestMessageHandlerConfiguration>(() =>
            {
                return desired;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count);
            Assert.AreSame(desired, data[typeof(TestMessageHandlerConfiguration)]);
        }

        [TestMethod]
        public void ConfigureFunc2RemoveTest()
        {
            var existing = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 20 }
            };
            var builder = new MessageHandlerConfigurationBuilder(existing);

            builder.Configure<TestMessageHandlerConfiguration>(config =>
            {
                Assert.IsNotNull(config);
                return null;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.Count);
        }

        [TestMethod]
        public void ConfigureFunc1RemoveTest()
        {
            var existing = new Dictionary<Type, object>
            {
                [typeof(TestMessageHandlerConfiguration)] = new TestMessageHandlerConfiguration { Int = 20 }
            };
            var builder = new MessageHandlerConfigurationBuilder(existing);

            builder.Configure<TestMessageHandlerConfiguration>(() =>
            {
                return null;
            });
            var handlerConfig = builder.Build();
            var data = handlerConfig.GetInternalData();

            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.Count);
        }
    }

    public static class TestMessageHandlerConfigurationExtension
    {
        public static ImmutableDictionary<Type, object> GetInternalData(this MessageHandlerConfiguration handlerConfiguration)
        {
            return (ImmutableDictionary<Type, object>)typeof(MessageHandlerConfiguration).GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(handlerConfiguration);
        }
    }
}
