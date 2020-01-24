using System;
using System.Linq;
using System.Reflection;
using AI4E.Messaging;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Serialization;
using AI4E.Utils.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore.Components.Modularity
{
    public static class MessagingBlazorModularityBuilderExtension
    {
        public static IBlazorModularityBuilder UseModuleMessaging(this IBlazorModularityBuilder builder)
        {
            var assembliesToLoad = new[]
            {
                typeof(System.ComponentModel.TypeConverter).Assembly,
                typeof(Newtonsoft.Json.JsonSerializer).Assembly,
                Assembly.GetExecutingAssembly()
            };

            return builder
                .ConfigureModuleServices(ConfigureMessagingServices)
                .LoadAssembliesInContext(assembliesToLoad);
        }

        public static IBlazorModularityBuilder UseModuleMessaging(
            this IBlazorModularityBuilder builder,
            Action<MessagingOptions> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            UseModuleMessaging(builder);

#pragma warning disable CA1062
            builder.Services.Configure(configuration);
#pragma warning restore CA1062

            return builder;
        }

        private static void ConfigureMessagingServices(ModuleContext context, IServiceCollection services)
        {
            var moduleDescriptor = context.ModuleDescriptor;
            var assemblyDescriptors = moduleDescriptor.Assemblies.Where(p => p.IsComponentAssembly);

            var partManager = new ApplicationPartManager();
            partManager.ApplicationParts.Clear();
            foreach (var assemblyDescriptor in assemblyDescriptors)
            {
                var assemblyName = assemblyDescriptor.GetAssemblyName();
                var assembly = context.ModuleLoadContext.LoadFromAssemblyName(assemblyName);

                partManager.ApplicationParts.Add(new AssemblyPart(assembly));
            }

            services.AddSingleton(partManager);
            services.AddMessaging(suppressRoutingSystem: true);

            var moduleName = context.ModuleDescriptor.Name;

            services.Configure<MessagingOptions>(options =>
            {
                options.LocalEndPoint = new RouteEndPointAddress(moduleName);
            });

            var messageSerializerAssembly = GetMessageSerializerAssembly(context);
            var messageSerializerType = messageSerializerAssembly.GetType(typeof(MessageSerializer).FullName!);

            services.AddSingleton(typeof(IMessageSerializer), messageSerializerType);
        }

        private static Assembly GetMessageSerializerAssembly(ModuleContext context)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();

            return context.ModuleLoadContext.LoadFromAssemblyName(assemblyName);
        }
    }
}
