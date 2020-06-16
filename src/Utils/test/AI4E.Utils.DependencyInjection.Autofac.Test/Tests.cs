using System;
using AI4E.Utils.DependencyInjection.Autofac.Test.TestTypes;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.DependencyInjection.Autofac.Test
{
    [TestClass]
    public class Tests
    {
        IServiceProvider ServiceProvider { get; set; }

        [TestInitialize]
        public void TestSetup()
        {
            var rootServices = new ServiceCollection();
            var rootContainerBuilder = new ContainerBuilder();
            ConfigureRootServices(rootServices);
            rootContainerBuilder.Populate(rootServices);
            var rootContainer = rootContainerBuilder.Build();
            ServiceProvider = new AutofacChildServiceProvider(rootContainer);
        }

        private void ConfigureRootServices(IServiceCollection services)
        {
            services.AddSingleton<SingletonService>();
            services.AddSingleton(typeof(SingletonService<>));
            services.AddScoped<ScopedService>();
            services.AddScoped(typeof(ScopedService<>));
            services.AddTransient<TransientService>();
            services.AddTransient(typeof(TransientService<>));

            services.AddSingleton<IOverridenSingletonService, OverridenSingletonService>();
            services.AddSingleton(typeof(IOverridenSingletonService<>), typeof(OverridenSingletonService<>));
            services.AddScoped<IOverridenScopedService, OverridenScopedService>();
            services.AddScoped(typeof(IOverridenScopedService<>), typeof(OverridenScopedService<>));
            services.AddTransient<IOverridenTransientService, OverridenTransientService>();
            services.AddTransient(typeof(IOverridenTransientService<>), typeof(OverridenTransientService<>));

            services.AddAutofacChildContainerBuilder();
        }

        private void ConfigureChildServices(IServiceCollection services)
        {
            services.AddSingleton<IOverridenSingletonService, OverridenSingletonServiceOverride>();
            services.AddSingleton(typeof(IOverridenSingletonService<>), typeof(OverridenSingletonServiceOverride<>));
            services.AddScoped<IOverridenScopedService, OverridenScopedServiceOverride>();
            services.AddScoped(typeof(IOverridenScopedService<>), typeof(OverridenScopedServiceOverride<>));
            services.AddTransient<IOverridenTransientService, OverridenTransientServiceOverride>();
            services.AddTransient(typeof(IOverridenTransientService<>), typeof(OverridenTransientServiceOverride<>));

            services.AddSingleton<ChildSingletonService>();
            services.AddSingleton(typeof(ChildSingletonService<>));
            services.AddScoped<ChildScopedService>();
            services.AddScoped(typeof(ChildScopedService<>));
            services.AddTransient<ChildTransientService>();
            services.AddTransient(typeof(ChildTransientService<>));
        }

        private IServiceProvider BuildChildContainer()
        {
            var childContainerBuilder = ServiceProvider.GetRequiredService<IChildContainerBuilder>();
            return childContainerBuilder.CreateChildContainer(ConfigureChildServices);

        }

        #region Resolve child service from root

        [TestMethod]
        public void ChildSingletonServiceNotResolvableFromRootTest()
        {
            var service = ServiceProvider.GetService<ChildSingletonService>();
            Assert.IsNull(service);
        }

        [TestMethod]
        public void GenericChildSingletonServiceNotResolvableFromRootTest()
        {
            var service = ServiceProvider.GetService<ChildSingletonService<int>>();
            Assert.IsNull(service);
        }

        [TestMethod]
        public void ChildTransientServiceNotResolvableFromRootTest()
        {
            var service = ServiceProvider.GetService<ChildTransientService>();
            Assert.IsNull(service);
        }

        [TestMethod]
        public void GenericChildTransientServiceNotResolvableFromRootTest()
        {
            var service = ServiceProvider.GetService<ChildTransientService<int>>();
            Assert.IsNull(service);
        }

        [TestMethod]
        public void ChildScopedServiceNotResolvableFromRootTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<ChildScopedService>();
            Assert.IsNull(service);
        }

        [TestMethod]
        public void GenericChildScopedServiceNotResolvableFromRootTest()
        {
            using var scope = ServiceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<ChildScopedService<int>>();
            Assert.IsNull(service);
        }

        #endregion

        #region Resolve root service from child

        [TestMethod]
        public void SingletonServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<SingletonService>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void GenericSingletonServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<SingletonService<int>>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void TransientServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<TransientService>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void GenericTransientServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<TransientService<int>>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void ScopedServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            ScopedService<int> service;

            using (var scope = childContainer.CreateScope())
            {
                service = scope.ServiceProvider.GetService<ScopedService<int>>();
            }

            Assert.IsNotNull(service);
            Assert.IsTrue(service.IsDisposed);
        }

        [TestMethod]
        public void GenericScopedServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            ScopedService<int> service;

            using (var scope = childContainer.CreateScope())
            {
                service = scope.ServiceProvider.GetService<ScopedService<int>>();
            }

            Assert.IsNotNull(service);
            Assert.IsTrue(service.IsDisposed);

        }

        #endregion

        #region Resolve child service from child

        [TestMethod]
        public void ChildSingletonServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<ChildSingletonService>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void GenericChildSingletonServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<ChildSingletonService<int>>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void ChildTransientServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<ChildTransientService>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void GenericChildTransientServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<ChildTransientService<int>>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void ChildScopedServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            using var scope = childContainer.CreateScope();
            var service = scope.ServiceProvider.GetService<ChildScopedService>();
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void GenericChildScopedServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            using var scope = childContainer.CreateScope();
            var service = scope.ServiceProvider.GetService<ChildScopedService<int>>();
            Assert.IsNotNull(service);
        }

        #endregion

        #region Resolve overriden service from child

        [TestMethod]
        public void OverridenSingletonServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<IOverridenSingletonService>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(OverridenSingletonServiceOverride));
        }

        [TestMethod]
        public void GenericOverridenSingletonServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<IOverridenSingletonService<int>>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(OverridenSingletonServiceOverride<int>));
        }

        [TestMethod]
        public void OverridenTransientServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<IOverridenTransientService>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(OverridenTransientServiceOverride));
        }

        [TestMethod]
        public void GenericOverridenTransientServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            var service = childContainer.GetService<IOverridenTransientService<int>>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(OverridenTransientServiceOverride<int>));
        }

        [TestMethod]
        public void OverridenScopedServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            using var scope = childContainer.CreateScope();
            var service = scope.ServiceProvider.GetService<IOverridenScopedService>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(OverridenScopedServiceOverride));
        }

        [TestMethod]
        public void GenericOverridenScopedServiceResolveFromChildTest()
        {
            var childContainer = BuildChildContainer();
            using var scope = childContainer.CreateScope();
            var service = scope.ServiceProvider.GetService<IOverridenScopedService<int>>();
            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(OverridenScopedServiceOverride<int>));
        }

        #endregion
    }
}
