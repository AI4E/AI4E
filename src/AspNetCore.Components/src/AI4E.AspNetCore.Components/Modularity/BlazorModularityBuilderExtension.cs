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
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.AspNetCore.Components.Modularity
{
    public static class BlazorModularityBuilderExtension
    {
        public static IBlazorModularityBuilder Configure(
            this IBlazorModularityBuilder builder,
            Action<BlazorModuleOptions> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CA1062
            builder.Services.Configure(configuration);
#pragma warning restore CA1062

            return builder;
        }

        public static IBlazorModularityBuilder UseDefaultModuleSource(this IBlazorModularityBuilder builder)
        {
#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

            services.AddSingleton<IBlazorModuleSourceFactory, BlazorModuleSourceFactory>();
            services.TryAddSingleton<IBlazorModuleAssemblyLoader, BlazorModuleAssemblyLoader>();

            return builder;
        }

        public static IBlazorModularityBuilder ConfigureCleanup(
            this IBlazorModularityBuilder builder,
            Func<ImmutableHashSet<Assembly>, ValueTask> cleanup)
        {
            if (cleanup is null)
                throw new ArgumentNullException(nameof(cleanup));

#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062


            services.Configure<BlazorModuleOptions>(options =>
            {
                options.ConfigureCleanup.Add(cleanup);
            });

            return builder;
        }

        public static IBlazorModularityBuilder ConfigureCleanup(
            this IBlazorModularityBuilder builder,
            Action<ImmutableHashSet<Assembly>> cleanup)
        {
            return ConfigureCleanup(builder, assemblies =>
            {
                cleanup(assemblies); return default;
            });
        }

        public static IBlazorModularityBuilder ConfigureModuleServices(
            this IBlazorModularityBuilder builder,
            Action<ModuleContext, IServiceCollection> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062


            services.Configure<BlazorModuleOptions>(options =>
            {
                options.ConfigureModuleServices.Add(configuration);
            });

            return builder;
        }

        public static IBlazorModularityBuilder ConfigureModuleServices(
            this IBlazorModularityBuilder builder,
            Action<IServiceCollection> configuration)
        {
            return ConfigureModuleServices(builder, (_, services) => configuration(services));
        }

        public static IBlazorModularityBuilder UseBlazorCachingWorkaround(this IBlazorModularityBuilder builder)
        {
            BlazorCachingWorkaround.Configure(builder);
            return builder;
        }

        public static IBlazorModularityBuilder UseAutofacCachingWorkaround(this IBlazorModularityBuilder builder)
        {
            AutofacCachingWorkaround.Configure(builder);
            return builder;
        }

        public static IBlazorModularityBuilder UseModuleSourceFactory<TBlazorModuleSourceFactory>(this IBlazorModularityBuilder builder)
            where TBlazorModuleSourceFactory : class, IBlazorModuleSourceFactory
        {
#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

            services.AddSingleton<IBlazorModuleSourceFactory, TBlazorModuleSourceFactory>();
            return builder;
        }

        public static IBlazorModularityBuilder UseModuleSourceFactory(
            this IBlazorModularityBuilder builder, IBlazorModuleSourceFactory moduleSourceFactory)
        {
#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

            services.AddSingleton(moduleSourceFactory);
            return builder;
        }

        public static IBlazorModularityBuilder UseModuleSourceFactory(
            this IBlazorModularityBuilder builder,
            Func<IServiceProvider, IBlazorModuleSourceFactory> implementationFactory)
        {
#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

            services.AddSingleton(implementationFactory);
            return builder;
        }

        public static IBlazorModularityBuilder ConfigureModuleSource(
           this IBlazorModularityBuilder builder,
           Func<IBlazorModuleSource, IBlazorModuleSource> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062


            services.Configure<BlazorModuleOptions>(options =>
            {
                options.ConfigureModuleSource.Add(configuration);
            });

            return builder;
        }

        public static IBlazorModularityBuilder ConfigureModuleSource(
           this IBlazorModularityBuilder builder,
           Func<IBlazorModuleDescriptor, IBlazorModuleDescriptor> configuration)
        {
            return builder.ConfigureModuleSource(moduleSource => moduleSource.Configure(configuration));
        }

        public static IBlazorModularityBuilder LoadAssembliesInContext(
            this IBlazorModularityBuilder builder,
            params Assembly[] assemblies)
        {
            return builder.ConfigureModuleSource(
                moduleDescriptor => moduleDescriptor.LoadAssembliesInContext(assemblies));
        }
    }
}
