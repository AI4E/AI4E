using System.Linq;
using AI4E.Storage.Projection.TestTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Projection
{
    [TestClass]
    public class ProjectionRegistryTests
    {
        [TestMethod]
        public void EmptyProviderTest()
        {
            var registry = new ProjectionRegistry();
            var provider = registry.ToProvider();

            Assert.AreEqual(0, provider.GetProjectionRegistrations().Count);
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
        }

        [TestMethod]
        public void RegisterTest()
        {
            var registration = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetProjectionRegistrations().Count);
            Assert.AreSame(registration, provider.GetProjectionRegistrations().First());
            Assert.AreEqual(1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
            Assert.AreSame(registration, provider.GetProjectionRegistrations(typeof(ProjectionSource)).First());
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void RegisterExistingTest()
        {
            var registration = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration);
            registry.Register(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetProjectionRegistrations().Count);
            Assert.AreSame(registration, provider.GetProjectionRegistrations().First());
            Assert.AreEqual(1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
            Assert.AreSame(registration, provider.GetProjectionRegistrations(typeof(ProjectionSource)).First());
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void RegisterOrderTest()
        {
            var registration1 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);
            var registration2 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration1);
            registry.Register(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(2, provider.GetProjectionRegistrations().Count);
            Assert.AreSame(registration2, provider.GetProjectionRegistrations().First());
            Assert.AreSame(registration1, provider.GetProjectionRegistrations().Last());
            Assert.AreEqual(2, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
            Assert.AreSame(registration2, provider.GetProjectionRegistrations(typeof(ProjectionSource)).First());
            Assert.AreSame(registration1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Last());
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void RegisterOrder2Test()
        {
            var registration1 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);
            var registration2 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration2);
            registry.Register(registration1);
            registry.Register(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(2, provider.GetProjectionRegistrations().Count);
            Assert.AreSame(registration2, provider.GetProjectionRegistrations().First());
            Assert.AreSame(registration1, provider.GetProjectionRegistrations().Last());
            Assert.AreEqual(2, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
            Assert.AreSame(registration2, provider.GetProjectionRegistrations(typeof(ProjectionSource)).First());
            Assert.AreSame(registration1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Last());
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void UnregisterTest()
        {
            var registration1 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);
            var registration2 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration2);
            registry.Register(registration1);
            registry.Unregister(registration1);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetProjectionRegistrations().Count);
            Assert.AreSame(registration2, provider.GetProjectionRegistrations().First());
            Assert.AreEqual(1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
            Assert.AreSame(registration2, provider.GetProjectionRegistrations(typeof(ProjectionSource)).First());
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void Unregister2Test()
        {
            var registration1 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);
            var registration2 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration2);
            registry.Register(registration1);
            registry.Unregister(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetProjectionRegistrations().Count);
            Assert.AreSame(registration1, provider.GetProjectionRegistrations().First());
            Assert.AreEqual(1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
            Assert.AreSame(registration1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).First());
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void UnregisterNonregisteredTest()
        {
            var registration1 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);
            var registration2 = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration1);
            registry.Unregister(registration2);
            var provider = registry.ToProvider();

            Assert.AreEqual(1, provider.GetProjectionRegistrations().Count);
            Assert.AreSame(registration1, provider.GetProjectionRegistrations().First());
            Assert.AreEqual(1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
            Assert.AreSame(registration1, provider.GetProjectionRegistrations(typeof(ProjectionSource)).First());
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(object)).Count);
        }

        [TestMethod]
        public void UnregisterNoneRegisteredTest()
        {
            var registration = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Unregister(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(0, provider.GetProjectionRegistrations().Count);
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
        }

        [TestMethod]
        public void UnregisterLastOfTypeTest()
        {
            var registration = new ProjectionRegistration(typeof(ProjectionSource), typeof(ProjectionTarget), _ => null);

            var registry = new ProjectionRegistry();
            registry.Register(registration);
            registry.Unregister(registration);
            var provider = registry.ToProvider();

            Assert.AreEqual(0, provider.GetProjectionRegistrations().Count);
            Assert.AreEqual(0, provider.GetProjectionRegistrations(typeof(ProjectionSource)).Count);
        }
    }
}
