using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AI4E.AspNetCore
{
    public static class WebHostExtension
    {
        public static async Task<IWebHost> InitializeApplicationServicesAsync(this IWebHost webhost, CancellationToken cancellation = default)
        {
            if (webhost == null)
                throw new ArgumentNullException(nameof(webhost));

            var serviceProvider = webhost.Services;
            await InitializeApplicationServicesAsync(serviceProvider, cancellation);

            return webhost;
        }

        private static async Task InitializeApplicationServicesAsync(IServiceProvider serviceProvider, CancellationToken cancellation)
        {
            var applicationServiceManager = serviceProvider.GetService<ApplicationServiceManager>();

            if (applicationServiceManager != null)
            {
                await applicationServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation);

                // Forces an asynchronous yield to the continuation that blocks synchronously
                // We do not want the contiuations of applicationServiceManager.InitializeApplicationServicesAsync to be blocked indefinitely
                await Task.Yield();
            }
        }

        public static async Task<IHost> InitializeApplicationServicesAsync(this IHost host, CancellationToken cancellation = default)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            var serviceProvider = host.Services;
            await InitializeApplicationServicesAsync(serviceProvider, cancellation);

            return host;
        }
    }
}
