using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

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
                object service;

                if (descriptors.Any(p => p.IsRequiredService))
                {
                    service = serviceProvider.GetRequiredService(serviceType);
                }
                else
                {
                    service = serviceProvider.GetService(serviceType);

                    if (service == null)
                    {
                        continue;
                    }
                }

                foreach (var descriptor in descriptors)
                {
                    tasks.Add(descriptor.ServiceInitialization(service, serviceProvider));
                }
            }

            return Task.WhenAll(tasks).WithCancellation(cancellation);
        }
    }

    public sealed class ApplicationServiceDescriptor
    {
        public ApplicationServiceDescriptor(Type serviceType, Func<object, IServiceProvider, Task> serviceInitialization, bool isRequiredService)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            ServiceType = serviceType;
            ServiceInitialization = serviceInitialization;
            IsRequiredService = isRequiredService;
        }

        public Type ServiceType { get; }
        public Func<object, IServiceProvider, Task> ServiceInitialization { get; }
        public bool IsRequiredService { get; }
    }

    public static class ApplicationServiceManagerExtension
    {
        public static void AddService(this ApplicationServiceManager applicationServiceManager,
                                      Type serviceType,
                                      Func<object, IServiceProvider, Task> serviceInitialization,
                                      bool isRequiredService = true)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(serviceType, serviceInitialization, isRequiredService));
        }

        public static void AddService(this ApplicationServiceManager applicationServiceManager,
                                      Type serviceType,
                                      Action<object, IServiceProvider> serviceInitialization,
                                      bool isRequiredService = true)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            Task ServiceInitialization(object service, IServiceProvider serviceProvider)
            {
                serviceInitialization(service, serviceProvider);

                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(serviceType, ServiceInitialization, isRequiredService));
        }

        public static void AddService(this ApplicationServiceManager applicationServiceManager,
                                      Type serviceType,
                                      bool isRequiredService = true)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            Task ServiceInitialization(object service, IServiceProvider serviceProvider)
            {
                if (service is IAsyncInitialization asyncInitialization)
                {
                    return asyncInitialization.Initialization;
                }

                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(serviceType, ServiceInitialization, isRequiredService));
        }

        public static void AddService<TService>(this ApplicationServiceManager applicationServiceManager,
                                                Func<TService, IServiceProvider, Task> serviceInitialization,
                                                bool isRequiredService = true)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            Task ServiceInitialization(object service, IServiceProvider serviceProvider)
            {
                return serviceInitialization((TService)service, serviceProvider);
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(typeof(TService), ServiceInitialization, isRequiredService));
        }

        public static void AddService<TService>(this ApplicationServiceManager applicationServiceManager,
                                                Action<TService, IServiceProvider> serviceInitialization,
                                                bool isRequiredService = true)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            if (serviceInitialization == null)
                throw new ArgumentNullException(nameof(serviceInitialization));

            Task ServiceInitialization(object service, IServiceProvider serviceProvider)
            {
                serviceInitialization((TService)service, serviceProvider);
                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(typeof(TService), ServiceInitialization, isRequiredService));
        }

        public static void AddService<TService>(this ApplicationServiceManager applicationServiceManager,
                                                bool isRequiredService = true)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            Task ServiceInitialization(object service, IServiceProvider serviceProvider)
            {
                var s = ((TService)service);

                if (s is IAsyncInitialization asyncInitialization)
                {
                    return asyncInitialization.Initialization;
                }

                return Task.CompletedTask;
            }

            applicationServiceManager.ApplicationServiceDescriptors.Add(new ApplicationServiceDescriptor(typeof(TService), ServiceInitialization, isRequiredService));
        }
    }
}
