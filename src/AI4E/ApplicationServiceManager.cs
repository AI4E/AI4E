using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E
{
    public sealed class ApplicationServiceManager
    {
        public IList<ApplicationServiceDescriptor> ApplicationServiceDescriptors { get; } = new List<ApplicationServiceDescriptor>();

        public Task InitializeApplicationServicesAsync(IServiceProvider serviceProvider, CancellationToken cancellation)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var tasks = new List<Task>();

            foreach (var (serviceType, descriptors) in ApplicationServiceDescriptors.Where(p => p != null).GroupBy(p => p.ServiceType).Select(p => (p.Key, p)))
            {
                var service = serviceProvider.GetRequiredService(serviceType);

                foreach (var descriptor in descriptors)
                {
                    tasks.Add(descriptor.ServiceInitialization(service));
                }
            }

            return Task.WhenAll(tasks).WithCancellation(cancellation);
        }
    }

    public sealed class ApplicationServiceDescriptor
    {
        public ApplicationServiceDescriptor(Type serviceType, Func<object, Task> serviceInitialization)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            ServiceType = serviceType;
            ServiceInitialization = serviceInitialization;
        }

        public Type ServiceType { get; }
        public Func<object, Task> ServiceInitialization { get; }
    }

    public static class ApplicationServiceManagerExtension
    {
        public static void AddService(this ApplicationServiceManager applicationServiceManager, Type serviceType, Func<object, Task> serviceInitialization)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(serviceType, serviceInitialization));
        }

        public static void AddService(this ApplicationServiceManager applicationServiceManager, Type serviceType, Action<object> serviceInitialization)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            Task ServiceInitialization(object service)
            {
                serviceInitialization(service);

                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(serviceType, ServiceInitialization));
        }

        public static void AddService(this ApplicationServiceManager applicationServiceManager, Type serviceType)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            Task ServiceInitialization(object service)
            {
                if (service is IAsyncInitialization asyncInitialization)
                {
                    return asyncInitialization.Initialization;
                }

                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(serviceType, ServiceInitialization));
        }

        public static void AddService<TService>(this ApplicationServiceManager applicationServiceManager, Func<TService, Task> serviceInitialization)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            Task ServiceInitialization(object service)
            {
                return serviceInitialization((TService)service);
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(typeof(TService), ServiceInitialization));
        }

        public static void AddService<TService>(this ApplicationServiceManager applicationServiceManager, Action<TService> serviceInitialization)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            Task ServiceInitialization(object service)
            {
                serviceInitialization((TService)service);
                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(typeof(TService), ServiceInitialization));
        }

        public static void AddService<TService>(this ApplicationServiceManager applicationServiceManager)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            Task ServiceInitialization(object service)
            {
                var s = ((TService)service);

                if (s is IAsyncInitialization asyncInitialization)
                {
                    return asyncInitialization.Initialization;
                }

                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(typeof(TService), ServiceInitialization));
        }
    }

    public static class ServiceCollectionExtension
    {
        public static void ConfigureApplicationServices(this IServiceCollection services, Action<ApplicationServiceManager> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var serviceManager = services.GetService<ApplicationServiceManager>();

            if (serviceManager == null)
            {
                serviceManager = new ApplicationServiceManager();
            }

            configuration(serviceManager);
            services.TryAddSingleton(serviceManager);
        }
    }
}
