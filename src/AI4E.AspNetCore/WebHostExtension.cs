using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore
{
    public static class WebHostExtension
    {
        public static async Task<IWebHost> InitializeApplicationServicesAsync(this IWebHost webhost, CancellationToken cancellation = default)
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
