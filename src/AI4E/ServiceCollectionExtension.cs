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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Handler;
using AI4E.Utils;
using AI4E.Utils.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E
{
    public static class ServiceCollectionExtension
    {
        public static void ConfigureApplicationServices(this IServiceCollection services, Action<ApplicationServiceManager> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var serviceManager = services.GetService<ApplicationServiceManager>();

            if (serviceManager == null)
            {
                serviceManager = new ApplicationServiceManager();
            }

            configuration(serviceManager);
            services.TryAddSingleton(serviceManager);
        }

        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
           
            services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();

            return services;
        }

        public static IMessagingBuilder AddInMemoryMessaging(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddMessageDispatcher<MessageDispatcher>();

            return new MessagingBuilder(services);
        }

        public static IMessagingBuilder AddInMemoryMessaging(this IServiceCollection services, Action<MessagingOptions> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var builder = services.AddInMemoryMessaging();
            builder.Services.Configure(configuration);
            return builder;
        }

        public static void AddMessageDispatcher<TMessageDispatcher>(this IServiceCollection services)
            where TMessageDispatcher : class, IMessageDispatcher
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.Decorate<IMessageHandlerRegistry>(BuildMessageHandlerRegistry);

            services.AddSingleton<IMessageDispatcher, TMessageDispatcher>();
        }

        public static void AddMessageDispatcher<TMessageDispatcher>(this IServiceCollection services, TMessageDispatcher instance)
            where TMessageDispatcher : class, IMessageDispatcher
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.Decorate<IMessageHandlerRegistry>(BuildMessageHandlerRegistry);

            services.AddSingleton<IMessageDispatcher>(instance);
        }

        public static void AddMessageDispatcher<TMessageDispatcher>(this IServiceCollection services, Func<IServiceProvider, TMessageDispatcher> factory)
            where TMessageDispatcher : class, IMessageDispatcher
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.Decorate<IMessageHandlerRegistry>(BuildMessageHandlerRegistry);

            services.AddSingleton<IMessageDispatcher>(factory);
        }

        public static void AddMessageDispatcher<TMessageDispatcher, TMessageDispatcherImpl>(this IServiceCollection services)
            where TMessageDispatcher : class, IMessageDispatcher
            where TMessageDispatcherImpl : class, TMessageDispatcher
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.Decorate<IMessageHandlerRegistry>(BuildMessageHandlerRegistry);

            services.AddSingleton<TMessageDispatcher, TMessageDispatcherImpl>();
            services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<TMessageDispatcher>());
        }

        public static void AddMessageDispatcher<TMessageDispatcher, TMessageDispatcherImpl>(this IServiceCollection services, TMessageDispatcherImpl instance)
            where TMessageDispatcher : class, IMessageDispatcher
            where TMessageDispatcherImpl : class, TMessageDispatcher
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.Decorate<IMessageHandlerRegistry>(BuildMessageHandlerRegistry);

            services.AddSingleton<TMessageDispatcher>(instance);
            services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<TMessageDispatcher>());
        }

        public static void AddMessageDispatcher<TMessageDispatcher, TMessageDispatcherImpl>(
            this IServiceCollection services,
            Func<IServiceProvider,
            TMessageDispatcherImpl> factory)
            where TMessageDispatcher : class, IMessageDispatcher
            where TMessageDispatcherImpl : class, TMessageDispatcher
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.Decorate<IMessageHandlerRegistry>(BuildMessageHandlerRegistry);

            services.AddSingleton<TMessageDispatcher>(factory);
            services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<TMessageDispatcher>());
        }

        public static IMessageHandlerRegistry BuildMessageHandlerRegistry(IMessageHandlerRegistry messageHandlerRegistry, IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (messageHandlerRegistry == null)
                throw new ArgumentNullException(nameof(messageHandlerRegistry));

            var options = serviceProvider.GetService<IOptions<MessagingOptions>>()?.Value ?? new MessagingOptions();
            var processors = options.MessageProcessors.ToImmutableArray();
            var partManager = serviceProvider.GetRequiredService<ApplicationPartManager>();
            var messageHandlerFeature = new MessageHandlerFeature();

            partManager.PopulateFeature(messageHandlerFeature);

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("MessageHandlerRegistration");
            RegisterMessageHandlerTypes(messageHandlerFeature.MessageHandlers, messageHandlerRegistry, processors, logger);

            return messageHandlerRegistry;
        }

        private static void RegisterMessageHandlerTypes(
            IEnumerable<Type> types,
            IMessageHandlerRegistry messageHandlerRegistry,
            ImmutableArray<IMessageProcessorRegistration> processors,
            ILogger logger)
        {
            foreach (var type in types)
            {
                RegisterMessageHandlerType(type, messageHandlerRegistry, processors, logger);
            }
        }

        private static void RegisterMessageHandlerType(
            Type handlerType,
            IMessageHandlerRegistry messageHandlerRegistry,
            ImmutableArray<IMessageProcessorRegistration> processors,
            ILogger logger)
        {
            var inspector = new MessageHandlerInspector(handlerType);
            var memberDescriptors = inspector.GetHandlerDescriptors();

            foreach (var memberDescriptor in memberDescriptors)
            {
                var registration = CreateMessageHandlerRegistration(memberDescriptor, processors);
                messageHandlerRegistry.Register(registration);

                logger?.LogDebug($"Registered handler of type '{handlerType}' for message-type '{memberDescriptor.MessageType}'.");
            }
        }

        private static IMessageHandlerRegistration CreateMessageHandlerRegistration(
            MessageHandlerActionDescriptor memberDescriptor,
            ImmutableArray<IMessageProcessorRegistration> processors)
        {
            var configuration = memberDescriptor.BuildConfiguration();

            return new MessageHandlerRegistration(
                memberDescriptor.MessageType,
                configuration,
                serviceProvider => MessageHandlerInvoker.CreateInvoker(memberDescriptor, processors, serviceProvider));
        }

        private static void ConfigureFeatureProviders(ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<MessageHandlerFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new MessageHandlerFeatureProvider());
            }
        }
    }
}
