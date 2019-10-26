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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Utils
{
    public sealed class ContextServicesDescriptor : IServiceProvider, IAsyncDisposable, IDisposable
    {
        private readonly string _context;
        private readonly ConcurrentDictionary<string, ContextServicesDescriptor>? _contextDescriptors;

        private readonly ContextServicesScopeFactory? _servicesScopeFactory;

        internal ContextServicesDescriptor(
            string context,
            IServiceProvider serviceProvider,
            IServiceProvider coreServiceProvider,
            ConcurrentDictionary<string, ContextServicesDescriptor> contextDescriptors)
        {
            _context = context;
            ServiceProvider = serviceProvider;
            _contextDescriptors = contextDescriptors;
            _servicesScopeFactory = new ContextServicesScopeFactory(this, coreServiceProvider);
        }

        internal ContextServicesDescriptor(string context, IServiceProvider serviceProvider)
        {
            _context = context;
            ServiceProvider = serviceProvider;
        }

        internal IServiceProvider ServiceProvider { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _contextDescriptors?.TryRemove(_context, this);
            }
            finally
            {
                // TODO: If this throws, all exceptions from above are lost.
                await ServiceProvider.DisposeIfDisposableAsync();
            }
        }

        public void Dispose()
        {
            try
            {
                _contextDescriptors?.TryRemove(_context, this);
            }
            finally
            {
                // TODO: If this throws, all exceptions from above are lost.
                ServiceProvider.DisposeIfDisposable();
            }
        }

        public object? GetService(Type serviceType)
        {
            if (_servicesScopeFactory != null &&
                serviceType == typeof(IServiceScopeFactory))
            {
                return _servicesScopeFactory;
            }

            return ServiceProvider.GetService(serviceType);
        }
    }
}
