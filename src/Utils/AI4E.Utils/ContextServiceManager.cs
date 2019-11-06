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
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Utils
{
    internal sealed class ContextServiceManager : IContextServiceManager, IAsyncDisposable, IDisposable
    {
        private static readonly IServiceProvider _emptyServiceProvider = new ServiceCollection().BuildServiceProvider();

        private readonly ImmutableArray<ServiceDescriptor> _coreServices;
        private readonly IServiceProvider _coreServiceProvider;
        private readonly ContextServiceProviderOptions _options;

        private ConcurrentDictionary<string, ContextServicesDescriptor>? _contextDescriptors;

        public ContextServiceManager(
            ImmutableArray<ServiceDescriptor> coreServices,
            IServiceProvider coreServiceProvider,
            ContextServiceProviderOptions options)
        {
            Debug.Assert(coreServiceProvider != null);
            Debug.Assert(options != null);

            _coreServices = coreServices;
            _coreServiceProvider = coreServiceProvider!;
            _options = options!;
            _contextDescriptors = new ConcurrentDictionary<string, ContextServicesDescriptor>();
        }

        public bool TryConfigureContextServices(
            string context,
            Action<IServiceCollection> serviceConfiguration,
            [NotNullWhen(true)] out ContextServicesDescriptor? contextDescriptor)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (serviceConfiguration is null)
                throw new ArgumentNullException(nameof(serviceConfiguration));

            var contextDescriptors = Volatile.Read(ref _contextDescriptors);

            if (contextDescriptors is null)
                throw new ObjectDisposedException(GetType().FullName);

            if (contextDescriptors.ContainsKey(context))
            {
                contextDescriptor = null;
                return false;
            }

            var serviceProvider = BuildServiceProvider(serviceConfiguration);
            contextDescriptor = new ContextServicesDescriptor(context, serviceProvider, _coreServiceProvider, contextDescriptors);

            if (!contextDescriptors.TryAdd(context, contextDescriptor))
            {
                contextDescriptor.Dispose();
                return false;
            }

            // Check whether the manager is disposed concurrently, to prevent the situation that we add a descriptor that never gets disposed.
            if (Volatile.Read(ref _contextDescriptors) is null)
            {
                contextDescriptor.Dispose();
                throw new ObjectDisposedException(GetType().FullName);
            }

            return true;
        }

        // TODO: Is there another solution than this ugly one?
        internal static ConcurrentDictionary<IServiceProvider, IServiceProvider> _scopedServiceProviderMappings
            = new ConcurrentDictionary<IServiceProvider, IServiceProvider>();

        private ServiceProvider BuildServiceProvider(Action<IServiceCollection> serviceConfiguration)
        {
            var serviceCollection = new ServiceCollection();

            for (var i = _coreServices.Length - 1; i >= 0; i--)
            {
                var coreServiceDescriptor = _coreServices[i];
                var coreServiceDescriptorType = coreServiceDescriptor.ServiceType;

                object CoreServiceResolver(IServiceProvider serviceProvider)
                {
                    if (!_scopedServiceProviderMappings.TryGetValue(serviceProvider, out var coreServiceProvider))
                    {
                        coreServiceProvider = _coreServiceProvider;
                    }

                    return coreServiceProvider.GetService(coreServiceDescriptorType);
                }

                ServiceDescriptor serviceDescriptor;

                if (coreServiceDescriptor.ImplementationInstance != null)
                {
                    serviceDescriptor = new ServiceDescriptor(
                        coreServiceDescriptor.ServiceType,
                        coreServiceDescriptor.ImplementationInstance);
                }

                // TODO: In case this is a singleton, each context gets its own instance, where actually
                //       we want to inherit the instance (per type-parameter) from the core (sharing the instance).
                else if (coreServiceDescriptor.ServiceType.IsGenericTypeDefinition)
                {
                    serviceDescriptor = new ServiceDescriptor(
                        coreServiceDescriptor.ServiceType,
                        coreServiceDescriptor.ImplementationType,
                        coreServiceDescriptor.Lifetime);
                }
                else
                {
                    serviceDescriptor = new ServiceDescriptor(
                        coreServiceDescriptor.ServiceType,
                        CoreServiceResolver,
                        coreServiceDescriptor.Lifetime);
                }

                serviceCollection.Insert(0, serviceDescriptor);
            }

            serviceConfiguration(serviceCollection);

            return serviceCollection.BuildServiceProvider(_options.ToServiceProviderOptions());
        }

        public ContextServicesDescriptor GetContextServices(string context, bool coreServicesIfNotFound = true)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            var contextDescriptors = Volatile.Read(ref _contextDescriptors);

            if (contextDescriptors is null)
                throw new ObjectDisposedException(GetType().FullName);

            if (contextDescriptors.TryGetValue(context, out var contextDescriptor))
            {
                return contextDescriptor;
            }

            if (coreServicesIfNotFound)
            {
                return new ContextServicesDescriptor(context, _coreServiceProvider);
            }

            return new ContextServicesDescriptor(context, _emptyServiceProvider);
        }

        public bool TryGetContextServices(string context, [NotNullWhen(true)] out ContextServicesDescriptor? serviceProvider)
        {
            var contextDescriptors = Volatile.Read(ref _contextDescriptors);

            if (contextDescriptors is null)
                throw new ObjectDisposedException(GetType().FullName);

            return contextDescriptors.TryGetValue(context, out serviceProvider);
        }

        public ValueTask DisposeAsync()
        {
            var contextDescriptors = Interlocked.Exchange(ref _contextDescriptors, null);

            if (contextDescriptors != null)
            {
                return contextDescriptors.Values
                    .Select(p => p.DisposeAsync())
                    .WhenAll();
            }

            return default;
        }

        public void Dispose()
        {
            var contextDescriptors = Interlocked.Exchange(ref _contextDescriptors, null);

            if (contextDescriptors != null)
            {
                contextDescriptors.Values
                    .Select(p => (Action)p.Dispose)
                    .Combine()
                    ?.InvokeAll();
            }
        }
    }
}
