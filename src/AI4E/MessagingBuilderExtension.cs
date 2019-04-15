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

namespace AI4E
{
    public static class MessagingBuilderExtension
    {
        public static IMessagingBuilder Configure(
            this IMessagingBuilder messagingBuilder,
            Action<MessagingOptions> configuration)
        {
            messagingBuilder.Services.Configure(configuration);

            return messagingBuilder;
        }

        public static IMessagingBuilder ConfigureMessageHandlers(
            this IMessagingBuilder messagingBuilder,
            Action<IMessageHandlerRegistry, IServiceProvider> configuration)
        {
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

        public static IMessagingBuilder UseDispatcher<TMessageDispatcher>(this IMessagingBuilder messagingBuilder)
            where TMessageDispatcher : class, IMessageDispatcher
        {
            var services = messagingBuilder.Services;

            services.UseDispatcher<TMessageDispatcher>();
            services.AddSingleton<TMessageDispatcher>();

            return messagingBuilder;
        }

        public static IMessagingBuilder UseDispatcher(
            this IMessagingBuilder messagingBuilder, IMessageDispatcher instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var services = messagingBuilder.Services;
            services.AddSingleton(instance);
            return messagingBuilder;
        }

        public static IMessagingBuilder UseDispatcher(
            this IMessagingBuilder messagingBuilder, Func<IServiceProvider, IMessageDispatcher> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var services = messagingBuilder.Services;
            services.AddSingleton(factory);
            return messagingBuilder;
        }

        public static IMessagingBuilder DecorateDispatcher<TMessageDispatcher>(
            this IMessagingBuilder messagingBuilder)
             where TMessageDispatcher : class, IMessageDispatcher
        {
            messagingBuilder.Services.Decorate<IMessageDispatcher, TMessageDispatcher>();
            return messagingBuilder;
        }

        public static IMessagingBuilder DecorateDispatcher(
            this IMessagingBuilder messagingBuilder,
            Func<IMessageDispatcher, IMessageDispatcher> decorator)
        {
            if (decorator == null)
                throw new ArgumentNullException(nameof(decorator));

            messagingBuilder.Services.Decorate(decorator);
            return messagingBuilder;
        }

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
