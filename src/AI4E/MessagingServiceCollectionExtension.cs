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
using AI4E;
using AI4E.Handler;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MessagingServiceCollectionExtension
    {
        public static IMessagingBuilder AddMessaging(this IServiceCollection services)
        {
            services.TryAddSingleton<IMessageHandlerRegistry, MessageHandlerRegistry>();
            services.TryAddSingleton<IMessageDispatcher, MessageDispatcher>();
            services.ConfigureApplicationParts(MessageHandlerFeatureProvider.Configure);

            var builder = new MessagingBuilder(services);
            MessageHandlers.Register(builder);
            return builder;
        }

        public static IMessagingBuilder AddMessaging(this IServiceCollection services, Action<MessagingOptions> configuration)
        {
            var builder = services.AddMessaging();
            builder.Services.Configure(configuration);
            return builder;
        }
    }
}
