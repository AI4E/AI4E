using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E
{
    [TestClass]
    public class MessageHandlerRegistryTests
    {
        [TestMethod]
        public void EmptyProviderTest()
        {
            var registry = new MessageHandlerRegistry();
            var provider = registry.ToProvider();

            Assert.AreEqual(0, provider.GetHandlerRegistrations().Count);
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(string)).Count);
        }

        [TestMethod]
        public void RegisterTest()
        {
            var registration = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetHandlerRegistrations().Count);
            Assert.AreSame(registration, provider.GetHandlerRegistrations().First());
            Assert.AreEqual(1, provider.GetHandlerRegistrations(typeof(string)).Count);
            Assert.AreSame(registration, provider.GetHandlerRegistrations(typeof(string)).First());
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void RegisterExistingTest()
        {
            var registration = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration);
            registry.Register(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetHandlerRegistrations().Count);
            Assert.AreSame(registration, provider.GetHandlerRegistrations().First());
            Assert.AreEqual(1, provider.GetHandlerRegistrations(typeof(string)).Count);
            Assert.AreSame(registration, provider.GetHandlerRegistrations(typeof(string)).First());
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void RegisterOrderTest()
        {
            var registration1 = new MessageHandlerRegistration(typeof(string), _ => null);
            var registration2 = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration1);
            registry.Register(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(2, provider.GetHandlerRegistrations().Count);
            Assert.AreSame(registration2, provider.GetHandlerRegistrations().First());
            Assert.AreSame(registration1, provider.GetHandlerRegistrations().Last());
            Assert.AreEqual(2, provider.GetHandlerRegistrations(typeof(string)).Count);
            Assert.AreSame(registration2, provider.GetHandlerRegistrations(typeof(string)).First());
            Assert.AreSame(registration1, provider.GetHandlerRegistrations(typeof(string)).Last());
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void RegisterOrder2Test()
        {
            var registration1 = new MessageHandlerRegistration(typeof(string), _ => null);
            var registration2 = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration2);
            registry.Register(registration1);
            registry.Register(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(2, provider.GetHandlerRegistrations().Count);
            Assert.AreSame(registration2, provider.GetHandlerRegistrations().First());
            Assert.AreSame(registration1, provider.GetHandlerRegistrations().Last());
            Assert.AreEqual(2, provider.GetHandlerRegistrations(typeof(string)).Count);
            Assert.AreSame(registration2, provider.GetHandlerRegistrations(typeof(string)).First());
            Assert.AreSame(registration1, provider.GetHandlerRegistrations(typeof(string)).Last());
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void UnregisterTest()
        {
            var registration1 = new MessageHandlerRegistration(typeof(string), _ => null);
            var registration2 = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration2);
            registry.Register(registration1);
            registry.Unregister(registration1);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetHandlerRegistrations().Count);
            Assert.AreSame(registration2, provider.GetHandlerRegistrations().First());
            Assert.AreEqual(1, provider.GetHandlerRegistrations(typeof(string)).Count);
            Assert.AreSame(registration2, provider.GetHandlerRegistrations(typeof(string)).First());
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void Unregister2Test()
        {
            var registration1 = new MessageHandlerRegistration(typeof(string), _ => null);
            var registration2 = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration2);
            registry.Register(registration1);
            registry.Unregister(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetHandlerRegistrations().Count);
            Assert.AreSame(registration1, provider.GetHandlerRegistrations().First());
            Assert.AreEqual(1, provider.GetHandlerRegistrations(typeof(string)).Count);
            Assert.AreSame(registration1, provider.GetHandlerRegistrations(typeof(string)).First());
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void UnregisterNonregisteredTest()
        {
            var registration1 = new MessageHandlerRegistration(typeof(string), _ => null);
            var registration2 = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration1);
            registry.Unregister(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetHandlerRegistrations().Count);
            Assert.AreSame(registration1, provider.GetHandlerRegistrations().First());
            Assert.AreEqual(1, provider.GetHandlerRegistrations(typeof(string)).Count);
            Assert.AreSame(registration1, provider.GetHandlerRegistrations(typeof(string)).First());
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void UnregisterNoneRegisteredTest()
        {
            var registration = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Unregister(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(0, provider.GetHandlerRegistrations().Count);
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(string)).Count);
        }

        [TestMethod]
        public void UnregisterLastOfTypeTest()
        {
            var registration = new MessageHandlerRegistration(typeof(string), _ => null);

            var registry = new MessageHandlerRegistry();
            registry.Register(registration);
            registry.Unregister(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(0, provider.GetHandlerRegistrations().Count);
            Assert.AreEqual(0, provider.GetHandlerRegistrations(typeof(string)).Count);
        }
    }
}
