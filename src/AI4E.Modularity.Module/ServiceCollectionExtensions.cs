using System;
using System.Net;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Modularity.Debug;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Module
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddModuleServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddOptions();
            services.AddUdpEndPoint();
            services.AddEndPointManager();
            services.AddMessageRouter();
            services.AddRemoteMessageDispatcher();
            services.AddSingleton(ConfigureLogicalEndPoint);
            services.AddSingleton(ConfigureCoordinationManager);
            services.AddSingleton(ConfigureDebugConnection);
            services.AddSingleton<IMetadataAccessor, MetadataAccessor>();
            services.AddSingleton<IModuleManager, ModuleManager>();
            services.AddSingleton<IMetadataReader, MetadataReader>();

            services.ConfigureApplicationServices(ConfigureApplicationServices);

            return services;
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<IMessageDispatcher>();
            serviceManager.AddService<DebugConnection>();
        }

        private static ICoordinationManager ConfigureCoordinationManager(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModuleDebugOptions();

            if (options.UseDebugConnection)
            {
                return ActivatorUtilities.CreateInstance<DebugCoordinationManager>(serviceProvider);
            }
            else
            {
                // TODO: Why is this restricted to IPEndPoints?
                return ActivatorUtilities.CreateInstance<CoordinationManager<IPEndPoint>>(serviceProvider);
            }
        }

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            Assert(serviceProvider != null);

            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModuleDebugOptions();
            var remoteOptionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var remoteOptions = remoteOptionsAccessor.Value ?? new RemoteMessagingOptions();

            if (remoteOptions.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            if (options.UseDebugConnection)
            {
                var debugConnection = serviceProvider.GetRequiredService<DebugConnection>();
                var logger = serviceProvider.GetService<ILogger<DebugLogicalEndPoint>>();
                return new DebugLogicalEndPoint(debugConnection, remoteOptions.LocalEndPoint, logger);
            }
            else
            {
                var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
                return endPointManager.CreateLogicalEndPoint(remoteOptions.LocalEndPoint);
            }
        }

        private static DebugConnection ConfigureDebugConnection(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModuleDebugOptions();

            if (!options.UseDebugConnection)
            {
                return null;
            }

            return ActivatorUtilities.CreateInstance<DebugConnection>(serviceProvider, optionsAccessor);
        }
    }

    internal static class ServiceCollectionHelper
    {
        private static IServiceCollection AddSingletonConditional<TService, TInstance, TOptions>(this IServiceCollection services, Func<TOptions, bool> condition)
           where TService : class
           where TInstance : class, TService
           where TOptions : class, new()
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            services.AddSingleton<TService, TInstance>(serviceProvider =>
            {
                var optionsAccessor = serviceProvider.GetService<IOptions<TOptions>>();
                var options = optionsAccessor?.Value ?? new TOptions();

                if (!condition(options))
                {
                    return null;
                }

                return ActivatorUtilities.CreateInstance<TInstance>(serviceProvider);
            });

            return services;
        }
    }
}
