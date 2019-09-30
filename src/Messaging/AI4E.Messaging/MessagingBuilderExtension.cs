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
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extensions for the <see cref="IMessagingBuilder"/> type.
    /// </summary>
    public static class MessagingBuilderExtension
    {
        /// <summary>
        /// Configures the messaging options.
        /// </summary>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <param name="configuration">A configuration for the messaging options.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
        public static IMessagingBuilder Configure(
            this IMessagingBuilder messagingBuilder,
            Action<MessagingOptions> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            messagingBuilder.Services.Configure(configuration);

            return messagingBuilder;
        }

        /// <summary>
        /// Configures the message handler that are registered with the messaging service.
        /// </summary>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <param name="configuration">A configuration that configures the registered message handlers.</param>
        /// <returns></returns>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
        public static IMessagingBuilder ConfigureMessageHandlers(
            this IMessagingBuilder messagingBuilder,
            Action<IMessageHandlerRegistry, IServiceProvider> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            messagingBuilder.Services.Decorate<IMessageHandlerRegistry>((registry, provider) =>
            {
                configuration(registry, provider);
                return registry;
            });

            return messagingBuilder;
        }

        private static void UseDispatcher<TMessageDispatcher>(this IServiceCollection services)
             where TMessageDispatcher : class, IMessageDispatcher
        {
            services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<TMessageDispatcher>());
        }

        /// <summary>
        /// Uses the message dispatcher of the specified type to dispatch messages.
        /// </summary>
        /// <typeparam name="TMessageDispatcher">The type of message dispatcher.</typeparam>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <remarks>This replaces the currently used message dispatcher.</remarks>
        public static IMessagingBuilder UseDispatcher<TMessageDispatcher>(this IMessagingBuilder messagingBuilder)
            where TMessageDispatcher : class, IMessageDispatcher
        {
            var services = messagingBuilder.Services;

            services.UseDispatcher<TMessageDispatcher>();
            services.AddSingleton<TMessageDispatcher>();

            return messagingBuilder;
        }

        /// <summary>
        /// Uses the specified message dispatcher.
        /// </summary>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <param name="instance">The message dispatcher that is used to dispatch messages.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <remarks>This replaces the currently used message dispatcher.</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="instance"/> is null.</exception>
        public static IMessagingBuilder UseDispatcher(
            this IMessagingBuilder messagingBuilder, IMessageDispatcher instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var services = messagingBuilder.Services;
            services.AddSingleton(instance);
            return messagingBuilder;
        }

        /// <summary>
        /// Uses the message dispatcher generated from the specified factory.
        /// </summary>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <param name="factory">The factory that is used to create the message dispatcher.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <remarks>This replaces the currently used message dispatcher.</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is null.</exception>
        public static IMessagingBuilder UseDispatcher(
            this IMessagingBuilder messagingBuilder, Func<IServiceProvider, IMessageDispatcher> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var services = messagingBuilder.Services;
            services.AddSingleton(factory);
            return messagingBuilder;
        }

        /// <summary>
        /// Decorates the message dispatcher with the specified type.
        /// </summary>
        /// <typeparam name="TMessageDispatcher">The type of message dispatcher decorator.</typeparam>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <remarks>This does NOT replace the currently used message dispatcher.</remarks>
        public static IMessagingBuilder DecorateDispatcher<TMessageDispatcher>(
            this IMessagingBuilder messagingBuilder)
             where TMessageDispatcher : class, IMessageDispatcher
        {
            messagingBuilder.Services.Decorate<IMessageDispatcher, TMessageDispatcher>();
            return messagingBuilder;
        }

        /// <summary>
        /// Decorates the message dispatcher.
        /// </summary>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <param name="decorator">The decorator that is used to decorate the current messaging dispatcher.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <remarks>This does NOT replace the currently used message dispatcher.</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="decorator"/> is null.</exception>
        public static IMessagingBuilder DecorateDispatcher(
            this IMessagingBuilder messagingBuilder,
            Func<IMessageDispatcher, IMessageDispatcher> decorator)
        {
            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            messagingBuilder.Services.Decorate(decorator);
            return messagingBuilder;
        }

        /// <summary>
        /// Decorates the message dispatcher.
        /// </summary>
        /// <param name="messagingBuilder">The messaging builder.</param>
        /// <param name="decorator">The decorator that is used to decorate the current messaging dispatcher.</param>
        /// <returns>A <see cref="IMessagingBuilder"/> used to configure the messaging service.</returns>
        /// <remarks>This does NOT replace the currently used message dispatcher.</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="decorator"/> is null.</exception>
        public static IMessagingBuilder DecorateDispatcher(
            this IMessagingBuilder messagingBuilder,
            Func<IMessageDispatcher, IServiceProvider, IMessageDispatcher> decorator)
        {
            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            messagingBuilder.Services.Decorate(decorator);
            return messagingBuilder;
        }
    }
}
