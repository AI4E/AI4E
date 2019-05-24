/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

using System;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E
{
    [TestClass]
    public class ApplicationServiceManagerTests
    {
        [TestMethod]
        public async Task BasicTest()
        {
            var service1 = new Service1();
            var service2 = new Service2();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            serviceCollection.AddSingleton(service2);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService(typeof(Service1), (obj, services) => ((Service1)obj).InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService(typeof(Service2), (obj, services) => ((Service2)obj).InitAsync(services).AsTask(), isRequiredService: true);

            await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);

            Assert.IsTrue(service1.IsInit);
            Assert.AreSame(serviceProvider, service1.ServiceProvider);

            Assert.IsTrue(service2.IsInit);
            Assert.AreSame(serviceProvider, service2.ServiceProvider);
        }

        [TestMethod]
        public async Task NonRequiredTest()
        {
            var service1 = new Service1();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService(typeof(Service1), (obj, services) => ((Service1)obj).InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService(typeof(Service2), (obj, services) => ((Service2)obj).InitAsync(services).AsTask(), isRequiredService: false);

            await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);

            Assert.IsTrue(service1.IsInit);
            Assert.AreSame(serviceProvider, service1.ServiceProvider);
        }

        [TestMethod]
        public async Task RequiredNonResolvableThrowsTest()
        {
            var service1 = new Service1();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService(typeof(Service1), (obj, services) => ((Service1)obj).InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService(typeof(Service2), (obj, services) => ((Service2)obj).InitAsync(services).AsTask(), isRequiredService: true);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);
            });
        }

        [TestMethod]
        public async Task ExceptionTest()
        {
            var service1 = new Service1();
            var service2 = new Service2();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            serviceCollection.AddSingleton(service2);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService(typeof(Service1), (obj, services) => ((Service1)obj).InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService(typeof(Service2), (obj, services) => ((Service2)obj).ThrowAsync(services).AsTask(), isRequiredService: true);

            await Assert.ThrowsExceptionAsync<CustomException>(async () =>
            {
                await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);
            });
        }

        [TestMethod]
        public async Task SynchronousInitTest()
        {
            var service1 = new Service1();
            var service2 = new Service2();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            serviceCollection.AddSingleton(service2);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService(typeof(Service1), (obj, services) => ((Service1)obj).InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService(typeof(Service2), (obj, services) => ((Service2)obj).Init(services), isRequiredService: true);

            await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);

            Assert.IsTrue(service1.IsInit);
            Assert.AreSame(serviceProvider, service1.ServiceProvider);

            Assert.IsTrue(service2.IsInit);
            Assert.AreSame(serviceProvider, service2.ServiceProvider);
        }

        [TestMethod]
        public async Task AsyncInitTest()
        {
            var service1 = new Service1();
            var service2 = new AsyncInitService();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            serviceCollection.AddSingleton(service2);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService(typeof(Service1), (obj, services) => ((Service1)obj).InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService(typeof(AsyncInitService), isRequiredService: true);

            await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);

            Assert.IsTrue(service1.IsInit);
            Assert.AreSame(serviceProvider, service1.ServiceProvider);

            Assert.IsTrue(service2.IsInit);
        }


        [TestMethod]
        public async Task GenericInit1Test()
        {
            var service1 = new Service1();
            var service2 = new Service2();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            serviceCollection.AddSingleton(service2);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService<Service1>((obj, services) => obj.InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService<Service2>((obj, services) => obj.Init(services), isRequiredService: true);

            await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);

            Assert.IsTrue(service1.IsInit);
            Assert.AreSame(serviceProvider, service1.ServiceProvider);

            Assert.IsTrue(service2.IsInit);
            Assert.AreSame(serviceProvider, service2.ServiceProvider);
        }

        [TestMethod]
        public async Task GenericInit2Test()
        {
            var service1 = new Service1();
            var service2 = new AsyncInitService();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(service1);
            serviceCollection.AddSingleton(service2);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var appServiceManager = new ApplicationServiceManager();
            appServiceManager.AddService<Service1>((obj, services) => obj.InitAsync(services).AsTask(), isRequiredService: true);
            appServiceManager.AddService<AsyncInitService>(isRequiredService: true);

            await appServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);

            Assert.IsTrue(service1.IsInit);
            Assert.AreSame(serviceProvider, service1.ServiceProvider);

            Assert.IsTrue(service2.IsInit);
        }

        private sealed class Service1
        {
            public bool IsInit { get; private set; }
            public IServiceProvider ServiceProvider { get; private set; }

            public ValueTask InitAsync(IServiceProvider serviceProvider)
            {
                IsInit = true;
                ServiceProvider = serviceProvider;
                return default;
            }
        }

        private sealed class Service2
        {
            public bool IsInit { get; private set; }
            public IServiceProvider ServiceProvider { get; private set; }

            public ValueTask InitAsync(IServiceProvider serviceProvider)
            {
                IsInit = true;
                ServiceProvider = serviceProvider;
                return default;
            }

            public ValueTask ThrowAsync(IServiceProvider serviceProvider)
            {
                IsInit = true;
                ServiceProvider = serviceProvider;
                throw new CustomException();
            }

            public void Init(IServiceProvider serviceProvider)
            {
                IsInit = true;
                ServiceProvider = serviceProvider;
            }
        }

        private sealed class AsyncInitService : IAsyncInitialization
        {
            private Task _init;

            public Task Initialization
            {
                get
                {
                    if (_init == null)
                        _init = InitAsync().AsTask();

                    return _init;
                }
            }

            public bool IsInit { get; private set; }

            public ValueTask InitAsync()
            {
                IsInit = true;
                return default;
            }
        }

        private sealed class CustomException : Exception { }
    }
}
