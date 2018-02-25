/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        ServiceCollectionExtension.cs 
 * Types:           AI4E.ServiceCollectionExtension
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   19.01.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * Asp.Net Core MVC
 * Copyright (c) .NET Foundation. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use
 * these files except in compliance with the License. You may obtain a copy of the
 * License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed
 * under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 * CONDITIONS OF ANY KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations under the License.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using AI4E.Internal;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AI4E
{
    public static class ServiceCollectionExtension
    {
        public static IMessagingBuilder AddMessaging(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Configure necessary application parts
            ConfigureApplicationParts(services);

            var mesageDispatcherRegistry = services.GetService<IMessageDispatcher>();

            // Configure services
            services.AddSingleton(provider => BuildMessageDispatcher(provider, mesageDispatcherRegistry));

            return new MessagingBuilder(services);
        }

        public static void ConfigureApplicationParts(IServiceCollection services)
        {
            var partManager = services.GetApplicationPartManager();
            partManager.ConfigureMessagingFeatureProviders();
            services.TryAddSingleton(partManager);
        }

        public static IMessagingBuilder AddMessaging(this IServiceCollection services, Action<MessagingOptions> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var builder = services.AddMessaging();
            builder.Services.Configure(configuration);
            return builder;
        }

        public static IMessageDispatcher BuildMessageDispatcher(IServiceProvider serviceProvider, IMessageDispatcher messageDispatcher)
        {
            if (messageDispatcher == null)
            {
                messageDispatcher = new MessageDispatcher(serviceProvider);
            }

            var options = serviceProvider.GetService<IOptions<MessagingOptions>>()?.Value;
            var processors = (options?.MessageProcessors ?? Enumerable.Empty<IContextualProvider<IMessageProcessor>>()).ToImmutableArray();

            var partManager = serviceProvider.GetRequiredService<ApplicationPartManager>();
            var messageHandlerFeature = new MessageHandlerFeature();

            partManager.PopulateFeature(messageHandlerFeature);

            foreach (var type in messageHandlerFeature.MessageHandlers)
            {
                var inspector = new MessageHandlerInspector(type);
                var descriptors = inspector.GetHandlerDescriptors();

                foreach (var descriptor in descriptors)
                {
                    var messageType = descriptor.MessageType;
                    var provider = Activator.CreateInstance(typeof(MessageHandlerProvider<>).MakeGenericType(messageType),
                                                            type,
                                                            descriptor,
                                                            processors);

                    var registerMethodDefinition = typeof(IMessageDispatcher).GetMethods()
                        .Single(p => p.Name == "Register" && p.IsGenericMethodDefinition && p.GetGenericArguments().Length == 1);

                    var registerMethod = registerMethodDefinition.MakeGenericMethod(messageType);

                    registerMethod.Invoke(messageDispatcher, new object[] { provider });
                }
            }

            return messageDispatcher;
        }

        private static void ConfigureMessagingFeatureProviders(this ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<MessageHandlerFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new MessageHandlerFeatureProvider());
            }
        }

        private static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            var manager = services.GetService<ApplicationPartManager>();
            if (manager == null)
            {
                manager = new ApplicationPartManager();
                var parts = DefaultAssemblyPartDiscoveryProvider.DiscoverAssemblyParts(Assembly.GetEntryAssembly().FullName);
                foreach (var part in parts)
                {
                    manager.ApplicationParts.Add(part);
                }
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
