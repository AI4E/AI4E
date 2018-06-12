/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Reflection;
using AI4E.Serialization;
using AI4E.Storage.Projection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;

namespace AI4E.Storage
{
    public static class ServiceCollectionExtension
    {
        public static IStorageBuilder AddStorage<TId, TEventBase, TEntityBase>(this IServiceCollection services)
            where TId : struct, IEquatable<TId>
            where TEventBase : class
            where TEntityBase : class
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Configure necessary application parts
            ConfigureApplicationParts(services);

            services.AddOptions();
            services.AddSingleton<ISerializer>(new Serialization.JsonSerializer());
            services.AddTransient<IStreamStore<string, TId>, StreamStore<string, TId>>();
            services.AddSingleton(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            });

            services.AddSingleton<ICommitDispatcher<string, TId>, EntityStore<TId, TEventBase, TEntityBase>.CommitDispatcher>();
            services.AddSingleton<ISnapshotProcessor<string, TId>, EntityStore<TId, TEventBase, TEntityBase>.SnapshotProcessor>();
            services.AddSingleton<IEntityAccessor<TId, TEventBase, TEntityBase>, DefaultEntityAccessor<TId, TEventBase, TEntityBase>>();
            services.AddTransient(provider => Provider.Create<EntityStore<TId, TEventBase, TEntityBase>>(provider));
            services.AddScoped<IEntityStore<TId, TEventBase, TEntityBase>>(
                provider => provider.GetRequiredService<IProvider<EntityStore<TId, TEventBase, TEntityBase>>>()
                                    .ProvideInstance());

            services.AddSingleton(typeof(IStreamPersistence<,>), typeof(StreamPersistence<,>));

            services.AddSingleton(BuildProjector);

            return new StorageBuilder(services);
        }

        public static IStorageBuilder AddStorage<TId, TEventBase, TEntityBase>(this IServiceCollection services, Action<StorageOptions> configuration)
            where TId : struct, IEquatable<TId>
            where TEventBase : class
            where TEntityBase : class
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var builder = services.AddStorage<TId, TEventBase, TEntityBase>();
            builder.Configure(configuration);

            return builder;
        }

        private static IProjector BuildProjector(IServiceProvider serviceProvider)
        {
            var projector = new Projector(serviceProvider);

            var partManager = serviceProvider.GetRequiredService<ApplicationPartManager>();
            var projectionFeature = new ProjectionFeature();

            partManager.PopulateFeature(projectionFeature);

            foreach (var type in projectionFeature.Projections)
            {
                var inspector = new ProjectionInspector(type);
                var descriptors = inspector.GetDescriptors();

                foreach (var descriptor in descriptors)
                {
                    var provider = Activator.CreateInstance(typeof(ProjectionInvoker<,>.Provider).MakeGenericType(descriptor.SourceType, descriptor.ProjectionType),
                                                            type,
                                                            descriptor);

                    var registerMethodDefinition = typeof(IProjector).GetMethods().Single(p => p.Name == "RegisterProjection" && p.IsGenericMethodDefinition && p.GetGenericArguments().Length == 2);
                    var registerMethod = registerMethodDefinition.MakeGenericMethod(descriptor.SourceType, descriptor.ProjectionType);
                    registerMethod.Invoke(projector, new object[] { provider });
                }
            }

            return projector;
        }

        private static void ConfigureApplicationParts(IServiceCollection services)
        {
            var partManager = services.GetApplicationPartManager();
            partManager.ConfigureMessagingFeatureProviders();
            services.TryAddSingleton(partManager);
        }

        private static void ConfigureMessagingFeatureProviders(this ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<ProjectionFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new ProjectionFeatureProvider());
            }
        }

        private static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            var manager = services.GetService<ApplicationPartManager>();
            if (manager == null)
            {
                manager = new ApplicationPartManager();
            }

            return manager;
        }

        private static T GetService<T>(this IServiceCollection services)
        {
            var serviceDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(T));

            return (T)serviceDescriptor?.ImplementationInstance;
        }
    }
}
