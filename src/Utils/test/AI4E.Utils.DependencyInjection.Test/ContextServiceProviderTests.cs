using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Test
{
    [TestClass]
    public sealed class ContextServiceProviderTests
    {
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
        }

        [TestCleanup]
        public void Teardown()
        {
            ContextServiceProvider.Dispose();
        }

        [TestMethod]
        public void ServiceLocatorTest()
        {
            var service = ContextServiceProvider.GetService(typeof(ServiceA));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ServiceA));
        }

        [TestMethod]
        public void ContextServiceManagerRequestTest()
        {
            var service = ContextServiceProvider.GetService(typeof(IContextServiceManager));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(IContextServiceManager));
        }
    }

    public class ServiceA { }

    public class ServiceB { }

    public class ServiceC { }
}
