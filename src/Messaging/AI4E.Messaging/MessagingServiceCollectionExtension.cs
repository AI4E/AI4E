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
using AI4E.Messaging;
using AI4E.Messaging.Handler;
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
        public static IMessagingBuilder AddMessaging(this IServiceCollection services)
        {
            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.TryAddSingleton<IMessageDispatcher, MessageDispatcher>();
            services.ConfigureApplicationParts(MessageHandlerFeatureProvider.Configure);

            var builder = new MessagingBuilder(services);
            MessageHandlers.Register(builder);
            return builder;
        }

        /// <summary>
        /// Adds the messaging service to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">Configures the messaging options.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
        public static IMessagingBuilder AddMessaging(this IServiceCollection services, Action<MessagingOptions> configuration)
        {
            var builder = services.AddMessaging();
            builder.Services.Configure(configuration);
            return builder;
        }
    }
}
