using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Domain
{
    [TestClass]
    public class DomainServiceResolverTests
    {
        [TestMethod]
        public void UninitializedTest()
        {
            var resolver = DomainServiceResolver.DomainServices;
            var service = resolver.GetService(typeof(TestService));
            Assert.IsNull(service);
        }

        [TestMethod]
        public void AmbientStateTest()
        {
            var services = new ServiceCollection();
            services.AddSingleton<TestService>();
            var serviceProvider = services.BuildServiceProvider();

            using (DomainServiceResolver.UseDomainServices(serviceProvider))
            {
                var resolver = DomainServiceResolver.DomainServices;
                var service = resolver.GetService(typeof(TestService));
                Assert.IsNotNull(service);
                Assert.IsInstanceOfType(service, typeof(TestService));
            }
        }

        [TestMethod]
        public void DisposalTest()
        {
            var services = new ServiceCollection();
            services.AddSingleton<TestService>();
            var serviceProvider = services.BuildServiceProvider();

            using (DomainServiceResolver.UseDomainServices(serviceProvider)) { }

            var resolver = DomainServiceResolver.DomainServices;
            var service = resolver.GetService(typeof(TestService));
            Assert.IsNull(service);
        }

        [TestMethod]
        public async Task AsyncAmbientStateTest()
        {
            var services = new ServiceCollection();
            services.AddSingleton<TestService>();
            var serviceProvider = services.BuildServiceProvider();

            await Task.Yield();

            using (DomainServiceResolver.UseDomainServices(serviceProvider))
            {
                await Task.Yield();
                var resolver = DomainServiceResolver.DomainServices;
                var service = resolver.GetService(typeof(TestService));
                Assert.IsNotNull(service);
                Assert.IsInstanceOfType(service, typeof(TestService));
            }
        }

        private sealed class TestService { }
    }
}
