using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.AspNetCore.Blazor.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor
{
    public static class WebAssemblyHostExtension
    {
        public static async Task<IWebAssemblyHost> InitializeApplicationServicesAsync(this IWebAssemblyHost webhost, CancellationToken cancellation = default)
        {
            if (webhost == null)
                throw new ArgumentNullException(nameof(webhost));

            var serviceProvider = webhost.Services;
            var applicationServiceManager = serviceProvider.GetService<ApplicationServiceManager>();

            if (applicationServiceManager != null)
            {
                await applicationServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation);
            }

            return webhost;
        }
    }
}
