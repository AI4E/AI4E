using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.ApplicationParts;
using AI4E.ApplicationParts.Utils;
using AI4E.Blazor.Components;
using AI4E.Blazor.Modularity;
using AI4E.Modularity.Debug;
using AI4E.Modularity.Host;
using AI4E.Routing.SignalR.Client;
using AI4E.Utils;
using BlazorSignalR;
using Microsoft.AspNetCore.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor
{
    public static class ServiceCollectionExtension
    {
        internal static readonly string _defaultHubUrl = "/MessageDispatcherHub"; // TODO: This should be configured only once.

        public static void AddBlazorMessageDispatcher(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSignalRMessageDispatcher(hubConnectionBuilder =>
            {
                hubConnectionBuilder.WithUrlBlazor(new Uri(_defaultHubUrl, UriKind.Relative));
            });
        }

        public static void AddBlazorModularity(this IServiceCollection services, Assembly entryAssembly, bool isServerSide)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (!isServerSide)
            {
                services.AddBlazorMessageDispatcher();

                services.AddScoped<IModulePropertiesLookup, RemoteModulePropertiesLookup>();
                services.AddSingleton<IInstallationSetManager, ClientInstallationSetManager>();
            }
            else
            {
                services.AddHttpClient();

                // The module host already installs a service of type IModulePropertiesLookup
                services.AddSingleton<IInstallationSetManager, ServerInstallationSetManager>();
            }

            services.AddScoped<IModuleAssemblyDownloader, ModuleAssemblyDownloader>();
            services.AddScoped<IModuleManifestProvider, ModuleManifestProvider>();
            services.AddSingleton<ViewExtensionRenderer>();

            services.ConfigureApplicationParts(partManager => ConfigureApplicationParts(partManager, entryAssembly));
            services.ConfigureApplicationServices(ConfigureApplicationServices);
        }

        public static void AddBlazorModularity(this IServiceCollection services, bool isServerSide)
        {
            AddBlazorModularity(services, Assembly.GetCallingAssembly(), isServerSide);
        }

        // https://github.com/Suchiman/BlazorDualMode
        private static void AddHttpClient(this IServiceCollection services)
        {
            if (!services.Any(x => x.ServiceType == typeof(HttpClient)))
            {
                // Setup HttpClient for server side in a client side compatible fashion
                services.AddScoped(s =>
                {
                    // Creating the URI helper needs to wait until the JS Runtime is initialized, so defer it.
                    var uriHelper = s.GetRequiredService<IUriHelper>();
                    return new HttpClient
                    {
                        BaseAddress = new Uri(uriHelper.GetBaseUri() ?? "http://localhost:5050/") // TODO
                    };
                });
            }
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<IMessageDispatcher>();
            serviceManager.AddService<IInstallationSetManager>(InitializeInstallationSetManagerAsync);
        }

        private static Task InitializeInstallationSetManagerAsync(IInstallationSetManager installationSetManager, IServiceProvider serviceProvider)
        {
            Task.Run(async () =>
            {
                var messageDispatcher = serviceProvider.GetRequiredService<IMessageDispatcher>();

                Console.WriteLine("Performing query for running debug modules.");
                var queryResult = await messageDispatcher.QueryAsync<IEnumerable<DebugModuleProperties>>(cancellation: default);

                if (!queryResult.IsSuccessWithResult<IEnumerable<DebugModuleProperties>>(out var debugModules))
                {
                    throw new Exception("Unable to query installation set."); // TODO: Exception type
                }

                foreach (var debugModule in debugModules)
                {
                    await installationSetManager.InstallAsync(debugModule.Module, cancellation: default);
                }
            }).HandleExceptions();

            return Task.CompletedTask;

            //var queryResult = await messageDispatcher.QueryAsync<ResolvedInstallationSet>(cancellation: default);

            //if (!queryResult.IsSuccessWithResult<ResolvedInstallationSet>(out var installationSet))
            //{
            //    throw new Exception("Unable to query installation set."); // TODO: Exception type
            //}

            //await installationSetManager.UpdateInstallationSetAsync(installationSet.Resolved.Select(p => p.Module), cancellation: default);
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager, Assembly entryAssembly)
        {
            partManager.FeatureProviders.Add(new ComponentFeatureProvider());
            partManager.FeatureProviders.Add(new ViewExtensionFeatureProvider());
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
            partManager.ApplicationParts.Add(new AssemblyPart(entryAssembly));
        }
    }
}
