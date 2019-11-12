using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Test
{
    [TestClass]
    public sealed class ContextServiceManagerTests
    {
        private IContextServiceManager ContextServiceManager { get; set; }
        private ContextServiceProvider ContextServiceProvider { get; set; }
        private IServiceCollection UnderlyingServiceCollection { get; set; }

        [TestInitialize]
        public void Setup()
        {
            UnderlyingServiceCollection = new ServiceCollection();
            UnderlyingServiceCollection.AddTransient<ServiceA>();
            UnderlyingServiceCollection.AddScoped<ServiceB>();
            UnderlyingServiceCollection.AddSingleton<ServiceC>();

            ContextServiceProvider = new ContextServiceProvider(UnderlyingServiceCollection);
            ContextServiceManager = (IContextServiceManager)ContextServiceProvider.GetService(typeof(IContextServiceManager));
        }

        [TestCleanup]
        public void Teardown()
        {
            ContextServiceProvider.Dispose();
        }

        [TestMethod]
        public void TryConfigureContextServicesNullContextTest()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ContextServiceManager.TryConfigureContextServices(null, _ => { }, out _));
        }

        [TestMethod]
        public void TryConfigureContextServicesNullServiceConfigurationTest()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ContextServiceManager.TryConfigureContextServices("x", null, out _));
        }

        [TestMethod]
        public void TryConfigureContextServicesTest()
        {
            var success = ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);

            Assert.IsTrue(success);
            Assert.IsNotNull(servicesDescriptor);
        }

        [TestMethod]
        public void TryConfigureContextServicesExistingContextTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out _);
            var success = ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);

            Assert.IsFalse(success);
            Assert.IsNull(servicesDescriptor);
        }

        [TestMethod]
        public void GetContextServicesExistingContextTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var expectedServicesDescriptor);

            var servicesDescriptor = ContextServiceManager.GetContextServices("x", coreServicesIfNotFound: false);

            Assert.AreSame(expectedServicesDescriptor, servicesDescriptor);
        }

        [TestMethod]
        public void GetContextServicesExistingContextCoreServicesIfNotFoundTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var expectedServicesDescriptor);

            var servicesDescriptor = ContextServiceManager.GetContextServices("x", coreServicesIfNotFound: true);

            Assert.AreSame(expectedServicesDescriptor, servicesDescriptor);
        }

        [TestMethod]
        public void GetContextServicesNonExistingContextTest()
        {
            var servicesDescriptor = ContextServiceManager.GetContextServices("x", coreServicesIfNotFound: false);

            Assert.IsNotNull(servicesDescriptor);
            Assert.IsNull(servicesDescriptor.GetService(typeof(ServiceA)));
        }

        [TestMethod]
        public void GetContextServicesNonExistingContextCoreServicesIfNotFoundTest()
        {
            var servicesDescriptor = ContextServiceManager.GetContextServices("x", coreServicesIfNotFound: true);

            Assert.IsNotNull(servicesDescriptor);
            Assert.IsNotNull(servicesDescriptor.GetService(typeof(ServiceA)));
            Assert.IsInstanceOfType(servicesDescriptor.GetService(typeof(ServiceA)), typeof(ServiceA));
        }


        [TestMethod]
        public void TryGetContextServicesExistingContextTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var expectedServicesDescriptor);

            var success = ContextServiceManager.TryGetContextServices("x", out var servicesDescriptor);

            Assert.IsTrue(success);
            Assert.AreSame(expectedServicesDescriptor, servicesDescriptor);
        }

        [TestMethod]
        public void TryGetContextServicesNonExistingContextTest()
        {
            var success = ContextServiceManager.TryGetContextServices("x", out var servicesDescriptor);

            Assert.IsFalse(success);
            Assert.IsNull(servicesDescriptor);
        }

        [TestMethod]
        public void ContextServiceDescriptorDisposeTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);
            servicesDescriptor.Dispose();
            var success = ContextServiceManager.TryGetContextServices("x", out _);

            Assert.IsFalse(success);
            Assert.ThrowsException<ObjectDisposedException>(() => servicesDescriptor.GetService(typeof(ServiceA)));
        }

        [TestMethod]
        public void ContextServiceDescriptorDoubleDisposeTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);
            servicesDescriptor.Dispose();
            servicesDescriptor.Dispose();
            var success = ContextServiceManager.TryGetContextServices("x", out _);

            Assert.IsFalse(success);
            Assert.ThrowsException<ObjectDisposedException>(() => servicesDescriptor.GetService(typeof(ServiceA)));
        }

        [TestMethod]
        public async Task ContextServiceDescriptorAsyncDisposeTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);
            await servicesDescriptor.DisposeAsync();
            var success = ContextServiceManager.TryGetContextServices("x", out _);

            Assert.IsFalse(success);
            Assert.ThrowsException<ObjectDisposedException>(() => servicesDescriptor.GetService(typeof(ServiceA)));
        }

        [TestMethod]
        public async Task ContextServiceDescriptorDoubleAsyncDisposeTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);
            await servicesDescriptor.DisposeAsync();
            await servicesDescriptor.DisposeAsync();
            var success = ContextServiceManager.TryGetContextServices("x", out _);

            Assert.IsFalse(success);
            Assert.ThrowsException<ObjectDisposedException>(() => servicesDescriptor.GetService(typeof(ServiceA)));
        }

        [TestMethod]
        public void ContextServiceProviderDisposeTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);
            ContextServiceProvider.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => servicesDescriptor.GetService(typeof(ServiceA)));
            Assert.ThrowsException<ObjectDisposedException>(() => ContextServiceManager.TryGetContextServices("x", out _));
            Assert.ThrowsException<ObjectDisposedException>(() => ContextServiceManager.GetContextServices("x"));
            Assert.ThrowsException<ObjectDisposedException>(() => ContextServiceManager.TryConfigureContextServices("x", _ => { }, out _));
        }

        [TestMethod]
        public async Task ContextServiceProviderDisposeAsyncTest()
        {
            ContextServiceManager.TryConfigureContextServices("x", _ => { }, out var servicesDescriptor);
            await ContextServiceProvider.DisposeAsync();

            Assert.ThrowsException<ObjectDisposedException>(() => servicesDescriptor.GetService(typeof(ServiceA)));
            Assert.ThrowsException<ObjectDisposedException>(() => ContextServiceManager.TryGetContextServices("x", out _));
            Assert.ThrowsException<ObjectDisposedException>(() => ContextServiceManager.GetContextServices("x"));
            Assert.ThrowsException<ObjectDisposedException>(() => ContextServiceManager.TryConfigureContextServices("x", _ => { }, out _));
        }
    }
}
