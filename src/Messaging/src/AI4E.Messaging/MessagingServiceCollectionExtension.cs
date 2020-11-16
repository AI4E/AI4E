/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using AI4E.Messaging;
using AI4E.Messaging.MessageHandlers;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Serialization;
using AI4E.Utils;
using AI4E.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extensions methods for the <see cref="IServiceCollection"/> enabling adding the messaging services.
    /// </summary>
    public static class MessagingServiceCollectionExtension
    {
        /// <summary>
        /// Adds the messaging service to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        public static IMessagingBuilder AddMessaging(
            this IServiceCollection services,
            bool suppressRoutingSystem = false)
        {
            var builder = services.GetService<MessagingBuilderImpl>();

            if (builder is null)
            {
                services.AddOptions();
                services.AddAssemblyRegistry();

                // Add global helpers if not already present.
                services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
                services.TryAddSingleton<ITypeResolver>(TypeResolver.Default);

                // Add messaging helper services
                services.AddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
                services.AddSingleton<IMessageSerializer, MessageSerializer>();
                services.AddSingleton<IMessageHandlerResolver, MessageHandlerResolver>();

                if (!suppressRoutingSystem)
                {
                    AddRoutingSystem(services);
                }

                // Add the messaging marker service.
                services.AddSingleton<MessagingMarkerService>();

                // Force the message-dispatcher to initialize on application startup.
                // This is needed to ensure handlers are available and registered in multi-process or networking setups.
                services.ConfigureApplicationServices(
                    manager => manager.AddService<IMessagingEngine>(
                        engine => engine.Initialization, isRequiredService: true));

                builder = new MessagingBuilderImpl(services)
                {
                    RoutingSystemSuppressed = suppressRoutingSystem
                };

                // Add the message handlers to the messaging system
                MessageHandlers.Register(builder);

                // Add the message dispatcher infrastructure
                builder.UseEngine<MessagingEngine>();
                services.AddScoped(CreateDispatcher);

                services.AddSingleton(builder);
            }
            else if (builder.RoutingSystemSuppressed && !suppressRoutingSystem)
            {
                AddRoutingSystem(services);
                builder.RoutingSystemSuppressed = false;
            }

            return builder;
        }

        private static IMessageDispatcher CreateDispatcher(IServiceProvider serviceProvider)
        {
            var engine = serviceProvider.GetRequiredService<IMessagingEngine>();
            return engine.CreateDispatcher(serviceProvider);
        }

        private static void AddRoutingSystem(IServiceCollection services)
        {
            services.AddSingleton<IMessageRouterFactory, MessageRouterFactory>();
            services.AddSingleton<IRoutingSystem, RoutingSystem>();
            services.AddSingleton<IRouteManager, RouteManager>();
        }

        /// <summary>
        /// Adds the messaging service to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">Configures the messaging options.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
        public static IMessagingBuilder AddMessaging(
            this IServiceCollection services,
            Action<MessagingOptions> configuration,
            bool suppressRoutingSystem = false)
        {
            var builder = services.AddMessaging(suppressRoutingSystem);
            builder.Services.Configure(configuration);
            return builder;
        }
    }

    internal sealed class MessagingMarkerService
    {
        public MessagingMarkerService(IServiceProvider rootServiceProvider)
        {
            if (rootServiceProvider is null)
                throw new ArgumentNullException(nameof(rootServiceProvider));

            RootServiceProvider = rootServiceProvider;
        }

        public IServiceProvider RootServiceProvider { get; }
    }
}
