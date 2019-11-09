using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Test
{
    [TestClass]
    public sealed class ContextServicesDescriptorTests
    {
        private IContextServiceManager ContextServiceManager { get; set; }
        private ContextServiceProvider ContextServiceProvider { get; set; }
        private IServiceCollection UnderlyingServiceCollection { get; set; }
        private ContextServicesDescriptor ContextServicesDescriptor { get; set; }

        [TestInitialize]
        public void Setup()
        {
            UnderlyingServiceCollection = new ServiceCollection();

            UnderlyingServiceCollection.AddTransient<ServiceB>();
            UnderlyingServiceCollection.AddScoped<ServiceA>();

            UnderlyingServiceCollection.AddTransient<ServiceA>();
            UnderlyingServiceCollection.AddScoped<ServiceB>();
            UnderlyingServiceCollection.AddSingleton<ServiceC>();

            UnderlyingServiceCollection.AddSingleton(new ServiceD(false));
            UnderlyingServiceCollection.AddSingleton(typeof(ServiceX<>));
            UnderlyingServiceCollection.AddSingleton(typeof(ServiceY<>));

            ContextServiceProvider = new ContextServiceProvider(UnderlyingServiceCollection);
            ContextServiceManager = (IContextServiceManager)ContextServiceProvider.GetService(typeof(IContextServiceManager));
            ContextServiceManager.TryConfigureContextServices("x", ConfigureContextServices, out var contextServiceDescriptor);
            ContextServicesDescriptor = contextServiceDescriptor;
        }

        [TestCleanup]
        public void Teardown()
        {
            ContextServiceProvider.Dispose();
        }

        private void ConfigureContextServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ContextServiceA>();
            serviceCollection.AddScoped<ContextServiceB>();
            serviceCollection.AddSingleton<ContextServiceC>();
            serviceCollection.AddSingleton(new ServiceD(true));
            serviceCollection.AddSingleton(typeof(ContextServiceX<>));
            serviceCollection.AddSingleton(typeof(ServiceY<>));
        }

        [TestMethod]
        public void GetTransientContextServiceTest()
        {
            var service = ContextServicesDescriptor.GetService(typeof(ContextServiceA));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ContextServiceA));
        }

        [TestMethod]
        public void GetScopedContextServiceTest()
        {
            using var scope = ContextServicesDescriptor.CreateScope();

            var service = scope.ServiceProvider.GetService(typeof(ContextServiceB));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ContextServiceB));
        }

        [TestMethod]
        public void GetSingletonContextServiceTest()
        {
            var service = ContextServicesDescriptor.GetService(typeof(ContextServiceC));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ContextServiceC));
        }

        [TestMethod]
        public void GetReplacedServiceTest()
        {
            var service = ContextServicesDescriptor.GetService(typeof(ServiceD));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ServiceD));
            Assert.IsTrue(((ServiceD)service).FromContext);
        }

        [TestMethod]
        public void GetGenericBaseServiceTest()
        {
            var service = ContextServicesDescriptor.GetService(typeof(ServiceX<int>));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ServiceX<int>));
        }

        [TestMethod]
        public void GetReplacedGenericServiceTest()
        {
            var service = ContextServicesDescriptor.GetService(typeof(ServiceY<int>));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ServiceY<int>));
        }

        [TestMethod]
        public void GetGenericServiceTest()
        {
            var service = ContextServicesDescriptor.GetService(typeof(ContextServiceX<int>));

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(ContextServiceX<int>));
        }
    }

    public class ServiceD
    {
        public ServiceD(bool fromContext)
        {
            FromContext = fromContext;
        }

        public bool FromContext { get; }
    }

    public class ContextServiceA
    {
        public ContextServiceA(ServiceA serviceA)
        {
            ServiceA = serviceA;
        }

        public ServiceA ServiceA { get; }
    }

    public class ContextServiceB
    {
        public ContextServiceB(ServiceB serviceB)
        {
            ServiceB = serviceB;
        }

        public ServiceB ServiceB { get; }
    }

    public class ContextServiceC
    {
        public ContextServiceC(ServiceC serviceC)
        {
            ServiceC = serviceC;
        }

        public ServiceC ServiceC { get; }
    }

    public class ServiceX<T> { }

    public class ServiceY<T> { }

    public class ContextServiceX<T>
    {
        public ContextServiceX(ServiceX<T> serviceX)
        {
            ServiceX = serviceX;
        }

        public ServiceX<T> ServiceX { get; }
    }
}
