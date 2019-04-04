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
                async Task InitializeApplicationServicesAsync()
                {
                    await applicationServiceManager.InitializeApplicationServicesAsync(serviceProvider, cancellation: default);

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
