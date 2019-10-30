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
using System.Threading.Tasks;
using AI4E;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Blazor.Hosting
{
    public static class ComponentsModularityWebAssemblyHostExtension
    {
        public static IWebAssemblyHost InitializeApplicationServices(this IWebAssemblyHost webhost)
        {
            if (webhost == null)
                throw new ArgumentNullException(nameof(webhost));

            var serviceProvider = webhost.Services;
            var applicationServiceManager = serviceProvider.GetService<ApplicationServiceManager>();

            if (applicationServiceManager != null)
            {
                async Task InitializeApplicationServicesAsync()
                {
                    await applicationServiceManager
                        .InitializeApplicationServicesAsync(serviceProvider, cancellation: default)
                        .ConfigureAwait(false);

                    // Forces an asynchronous yield to the continuation that blocks synchronously
                    // We do not want the contiuations of applicationServiceManager.InitializeApplicationServicesAsync to be blocked indefinitely
                    await Task.Yield();
                }

                // We cannot wait for the result currently, as this blocks the JsRuntime to be initialized that we need in the app-services.
                // https://github.com/AI4E/AI4E/issues/39
                InitializeApplicationServicesAsync()
                    .ConfigureAwait(false)
                    //.GetAwaiter()
                    //.GetResult()
                    ;
            }

            return webhost;
        }
    }
}
