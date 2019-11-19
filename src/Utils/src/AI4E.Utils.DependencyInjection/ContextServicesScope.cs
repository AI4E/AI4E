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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Utils
{
    internal sealed class ContextServicesScope : IServiceScope
    {
        private readonly IServiceScope _servicesDescriptorScope;
        private readonly IServiceScope _coreServiceProviderScope;

        public ContextServicesScope(
            IServiceScope servicesDescriptorScope,
            IServiceScope coreServiceProviderScope)
        {
            _servicesDescriptorScope = servicesDescriptorScope;
            _coreServiceProviderScope = coreServiceProviderScope;

            ServiceProvider = servicesDescriptorScope.ServiceProvider;
            CoreServiceProvider = coreServiceProviderScope.ServiceProvider;

            ContextServiceManager._scopedServiceProviderMappings[ServiceProvider] = CoreServiceProvider;
        }

        public IServiceProvider ServiceProvider { get; }
        public IServiceProvider CoreServiceProvider { get; }

        public void Dispose()
        {
            ContextServiceManager._scopedServiceProviderMappings.TryRemove(ServiceProvider, CoreServiceProvider);
            _servicesDescriptorScope.Dispose();
            _coreServiceProviderScope.Dispose();
        }
    }

    internal sealed class ContextServicesScopeFactory : IServiceScopeFactory
    {
        private readonly ContextServicesDescriptor _servicesDescriptor;
        private readonly IServiceProvider _coreServiceProvider;

        public ContextServicesScopeFactory(
            ContextServicesDescriptor servicesDescriptor,
            IServiceProvider coreServiceProvider)
        {
            _servicesDescriptor = servicesDescriptor;
            _coreServiceProvider = coreServiceProvider;
        }

        public IServiceScope CreateScope()
        {
            return new ContextServicesScope(
                _servicesDescriptor.ServiceProvider.CreateScope(),
                _coreServiceProvider.CreateScope());
        }
    }
}
