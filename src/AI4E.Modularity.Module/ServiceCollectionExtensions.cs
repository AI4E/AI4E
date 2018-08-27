using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Modularity.Debug;
using AI4E.Proxying;
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
            services.AddSingleton(ConfigureProxyHost);
            services.AddSingleton<IMetadataAccessor, MetadataAccessor>();
            services.AddSingleton<IRunningModuleLookup, RunningModuleLookup>();
            services.AddSingleton<IMetadataReader, MetadataReader>();

            services.ConfigureApplicationServices(ConfigureApplicationServices);

            return services;
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<IMessageDispatcher>();
            serviceManager.AddService<ProxyHost>(isRequiredService: false);
        }

        private static ICoordinationManager ConfigureCoordinationManager(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

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

            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();
            var remoteOptionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var remoteOptions = remoteOptionsAccessor.Value ?? new RemoteMessagingOptions();

            if (remoteOptions.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            if (options.UseDebugConnection)
            {
                var proxyHost = serviceProvider.GetRequiredService<ProxyHost>();
                return new DebugLogicalEndPoint(proxyHost, remoteOptions.LocalEndPoint);
            }
            else
            {
                var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
                return endPointManager.GetLogicalEndPoint(remoteOptions.LocalEndPoint);
            }
        }

        private static ProxyHost ConfigureProxyHost(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (!options.UseDebugConnection)
            {
                return null;
            }

            var addressSerializer = serviceProvider.GetRequiredService<IAddressConversion<IPEndPoint>>();
            var logger = serviceProvider.GetService<ILogger<DisposeAwareStream>>();
            var endPoint = addressSerializer.Parse(options.DebugConnection);

            var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
            var tcpClient = new TcpClient(endPoint.AddressFamily);

            // TODO: Logging
            tcpClient.Connect(endPoint.Address, endPoint.Port); // TODO: This is a blocking call
            var stream = new DisposeAwareStream(tcpClient.GetStream(), dateTimeProvider, () => { Environment.FailFast(""); return Task.CompletedTask; }, logger); // TODO: Graceful shutdown

            return new ProxyHost(stream, serviceProvider);
        }
    }
}
