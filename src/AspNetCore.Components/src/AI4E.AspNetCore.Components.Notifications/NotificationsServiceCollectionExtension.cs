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
using AI4E.AspNetCore.Components.Notifications;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains notification specific extensions of the <see cref="IServiceCollection"/> type.
    /// </summary>
    public static class NotificationsServiceCollectionExtension
    {
        /// <summary>
        /// Adds the notifications service to the service-collection.
        /// </summary>
        /// <param name="services">The service-collection.</param>
        /// <returns>The service-collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is <c>null</c>.</exception>
        public static IServiceCollection AddNotifications(this IServiceCollection services)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddOptions();

            services.AddScoped<NotificationManager>();
            services.AddScoped<INotificationManager>(p => p.GetRequiredService<NotificationManager>());
            services.AddScoped<INotificationManager<Notification>>(p => p.GetRequiredService<NotificationManager>());
            return services;
        }

        /// <summary>
        /// Adds the notifications service to the service-collection.
        /// </summary>
        /// <param name="services">The service-collection.</param>
        /// <param name="configuration">A callback that configures the notification options.</param>
        /// <returns>The service-collection.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        public static IServiceCollection AddNotifications(
            this IServiceCollection services,
            Action<NotificationOptions> configuration)
        {
            return AddNotifications(services).Configure(configuration);
        }
    }
}
