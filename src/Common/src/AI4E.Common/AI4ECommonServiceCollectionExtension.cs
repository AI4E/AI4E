/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using AI4E;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AI4ECommonServiceCollectionExtension
    {
        public static IServiceCollection AddAssemblyRegistry(this IServiceCollection services)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IAssemblyRegistry, AssemblyRegistry>();

            return services;
        }

        public static IServiceCollection AddAssemblyRegistry(
            this IServiceCollection services, Action<IAssemblyRegistry> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            AddAssemblyRegistry(services);

            services.Decorate<IAssemblyRegistry>(registry =>
            {
                configuration(registry);
                return registry;
            });

            return services;
        }

        public static IServiceCollection AddAssemblyRegistry(
           this IServiceCollection services, Action<IAssemblyRegistry, IServiceProvider> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            AddAssemblyRegistry(services);

            services.Decorate<IAssemblyRegistry>((registry, serviceProvider) =>
            {
                configuration(registry, serviceProvider);
                return registry;
            });

            return services;
        }

        public static IServiceCollection ConfigureAssemblyRegistry(
            this IServiceCollection services, Action<IAssemblyRegistry> configuration)
        {
            return AddAssemblyRegistry(services, configuration);
        }

        public static IServiceCollection ConfigureAssemblyRegistry(
            this IServiceCollection services, Action<IAssemblyRegistry, IServiceProvider> configuration)
        {
            return AddAssemblyRegistry(services, configuration);
        }
    }
}
