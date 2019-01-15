using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Blazor.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor
{
    public static class WebAssemblyHostExtension
    {
        public static IWebAssemblyHost InitializeApplicationServices(this IWebAssemblyHost webhost)
        {
            if (webhost == null)
                throw new ArgumentNullException(nameof(webhost));

            var serviceProvider = webhost.Services;
            var applicationServiceManager = serviceProvider.GetService<ApplicationServiceManager>();

            if (applicationServiceManager != null)
            {
                // TODO: https://github.com/AI4E/AI4E/issues/39
                //       There seems to be a dead-lock with the initialization of the messaging infrastructure if this is not off-loaded.
                //       This does not seem to be true. ANY asnyc initialization that is not off-loaded locks up the process. This seems to be a live lock,
                //       as there are many CPU cycles burnt without progress.
                Task.Run(() => applicationServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default));
            }

            return webhost;
        }
    }
}
