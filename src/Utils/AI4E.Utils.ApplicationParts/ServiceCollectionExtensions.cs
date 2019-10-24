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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * Asp.Net Core MVC
 * Copyright (c) .NET Foundation. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use
 * these files except in compliance with the License. You may obtain a copy of the
 * License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed
 * under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 * CONDITIONS OF ANY KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations under the License.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Reflection;
using AI4E.Utils.ApplicationParts;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring the application part manager.
    /// </summary>
    public static class AI4EUtilsApplicationPartsServiceCollectionExtensions
    {
        /// <summary>
        /// Configured the application part manager with the specified configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application part manager configuration.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.</exception>
        public static IServiceCollection ConfigureApplicationParts(
            this IServiceCollection services,
            Action<ApplicationPartManager> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            ApplicationPartManager DecoratePartManager(ApplicationPartManager partManager)
            {
                configuration(partManager);
                return partManager;
            }

            if (!services.TryDecorate<ApplicationPartManager>(DecoratePartManager))
            {
                var partManager = new ApplicationPartManager();
                var entryAssembly = Assembly.GetEntryAssembly();

                // Blazor cannot access the entry assembly apparently.
                if (entryAssembly != null)
                {
                    partManager.ApplicationParts.Add(new AssemblyPart(entryAssembly));
                }

                configuration(partManager);
                services.AddSingleton(partManager);
            }

            return services;
        }
    }
}
