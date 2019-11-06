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
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Utils
{
    public sealed class ContextServiceProvider : IServiceProvider, IAsyncDisposable, IDisposable
    {
        private readonly ImmutableArray<ServiceDescriptor> _coreServices;
        private readonly IServiceProvider _coreServiceProvider;

        public ContextServiceProvider(IServiceCollection services, ContextServiceProviderOptions options)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _coreServices = services.ToImmutableArray();
            services.AddSingleton<IContextServiceManager>(_ => new ContextServiceManager(_coreServices, _coreServiceProvider, options));
            _coreServiceProvider = services.BuildServiceProvider(options.ToServiceProviderOptions());
        }

        public ContextServiceProvider(
            IServiceCollection services, bool validateScopes = true)
            : this(services, new ContextServiceProviderOptions { ValidateScopes = validateScopes })
        { }

        public object? GetService(Type serviceType)
        {
            return _coreServiceProvider.GetService(serviceType);
        }

        public async ValueTask DisposeAsync()
        {
            await _coreServiceProvider.DisposeIfDisposableAsync();
        }

        public void Dispose()
        {
            _coreServiceProvider.DisposeIfDisposable();
        }
    }

    public sealed class ContextServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
    {
        private readonly ContextServiceProviderOptions _options;

        public ContextServiceProviderFactory() : this(ContextServiceProviderOptions.Default)
        { }

        public ContextServiceProviderFactory(ContextServiceProviderOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _options = options;
        }

        public IServiceCollection CreateBuilder(IServiceCollection services)
        {
            return services;
        }

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            return new ContextServiceProvider(containerBuilder, _options);
        }
    }

    public sealed class ContextServiceProviderOptions
    {
        internal static ContextServiceProviderOptions Default { get; } = new ContextServiceProviderOptions();

        /// <summary>
        /// <c>true</c> to perform check verifying that scoped services never gets resolved from root provider; 
        /// otherwise <c>false</c>. Defaults to <c>false</c>.
        /// </summary>
        public bool ValidateScopes { get; set; }

        /// <summary>
        /// <c>true</c> to perform check verifying that all services can be created during
        /// <code>BuildContextServiceProvider</code> call; otherwise <c>false</c>. Defaults to <c>false</c>.
        /// NOTE: this check doesn't verify open generics services.
        /// </summary>
        public bool ValidateOnBuild { get; set; }

        internal ServiceProviderOptions ToServiceProviderOptions()
        {
            return new ServiceProviderOptions
            {
                ValidateOnBuild = ValidateOnBuild,
                ValidateScopes = ValidateScopes
            };
        }
    }
}
