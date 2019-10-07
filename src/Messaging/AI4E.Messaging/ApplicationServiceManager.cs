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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E
{
    /// <summary>
    /// Manages the intialization of application services.
    /// </summary>
    [Obsolete("Move to Utils")]
    public sealed class ApplicationServiceManager
    {
        /// <summary>
        /// Gets a list of registered allocation service descriptors.
        /// </summary>
        public IList<ApplicationServiceDescriptor> ApplicationServiceDescriptors { get; } = new List<ApplicationServiceDescriptor>();

        /// <summary>
        /// Asychronously initializes all registered application services.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to access services.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Describes an application service.
    /// </summary>
    public sealed class ApplicationServiceDescriptor
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ApplicationServiceDescriptor"/> type.
        /// </summary>
        /// <param name="serviceType">The type of application service.</param>
        /// <param name="serviceInitialization">The asynchronous application service initialization.</param>
        /// <param name="isRequiredService">A boolean value indicating whether the application service is mandatory.</param>
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

        /// <summary>
        /// Gets the type of application service.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Gets the asynchronous application service initialization.
        /// </summary>
        public Func<object, IServiceProvider, Task> ServiceInitialization { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the application service is mandatory.
        /// </summary>
        public bool IsRequiredService { get; }
    }

    /// <summary>
    /// Contains extensions for the <see cref="ApplicationServiceManager"/> type.
    /// </summary>
    public static class ApplicationServiceManagerExtension
    {
        /// <summary>
        /// Adds an application service to the manager.
        /// </summary>
        /// <param name="applicationServiceManager">The application service manager.</param>
        /// <param name="serviceType">The application service type.</param>
        /// <param name="serviceInitialization">The asynchronous application service initialization. </param>
        /// <param name="isRequiredService">A boolean value indicating whether the application service is mandatory.</param>
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

        /// <summary>
        /// Adds an application service to the manager.
        /// </summary>
        /// <param name="applicationServiceManager">The application service manager.</param>
        /// <param name="serviceType">The application service type.</param>
        /// <param name="serviceInitialization">The asynchronous application service initialization. </param>
        /// <param name="isRequiredService">A boolean value indicating whether the application service is mandatory.</param>
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

        /// <summary>
        /// Adds an application service to the manager.
        /// </summary>
        /// <param name="applicationServiceManager">The application service manager.</param>
        /// <param name="serviceType">The application service type.</param>
        /// <param name="isRequiredService">A boolean value indicating whether the application service is mandatory.</param>
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

        /// <summary>
        /// Adds an application service to the manager.
        /// </summary>
        /// <typeparam name="TService">The application service type.</typeparam>
        /// <param name="applicationServiceManager">The application service manager.</param>
        /// <param name="serviceInitialization">The asynchronous application service initialization. </param>
        /// <param name="isRequiredService">A boolean value indicating whether the application service is mandatory.</param>
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


        /// <summary>
        /// Adds an application service to the manager.
        /// </summary>
        /// <typeparam name="TService">The application service type.</typeparam>
        /// <param name="applicationServiceManager">The application service manager.</param>
        /// <param name="serviceInitialization">The synchronous application service initialization. </param>
        /// <param name="isRequiredService">A boolean value indicating whether the application service is mandatory.</param>
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

        /// <summary>
        /// Adds an application service to the manager.
        /// </summary>
        /// <typeparam name="TService">The application service type.</typeparam>
        /// <param name="applicationServiceManager">The application service manager.</param>
        /// <param name="isRequiredService">A boolean value indicating whether the application service is mandatory.</param>
        public static void AddService<TService>(this ApplicationServiceManager applicationServiceManager,
                                                bool isRequiredService = true)
        {
            if (applicationServiceManager == null)
                throw new ArgumentNullException(nameof(applicationServiceManager));

            Task ServiceInitialization(object service, IServiceProvider serviceProvider)
            {
                var s = (TService)service;

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
