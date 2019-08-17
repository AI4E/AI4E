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
using AI4E;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extensions for the <see cref="IServiceCollection"/> type.
    /// </summary>
    public static class ApplicationServiceManagerServiceCollectionExtension
    {
        /// <summary>
        /// Configures the application service manager.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application service manager configuration.</param>
        /// <returns>The service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is <c>null</c>.</exception>
        public static IServiceCollection ConfigureApplicationServices(this IServiceCollection services, Action<ApplicationServiceManager> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var serviceManager = services.GetService<ApplicationServiceManager>();

            if (serviceManager == null)
            {
                serviceManager = new ApplicationServiceManager();
            }

            configuration(serviceManager);
            services.TryAddSingleton(serviceManager);

            return services;
        }
    }
}
