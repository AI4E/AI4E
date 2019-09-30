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
using System.Linq;
using AI4E.Utils.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.Messaging.Handler
{
    internal static class MessageHandlers
    {
        public static void Register(MessagingBuilder builder)
        {
            // Protect us from registering the message-handlers multiple times.
            if (builder.Services.Any(p => p.ServiceType == typeof(MessageHandlersRegisteredMarker)))
                return;

            builder.Services.AddSingleton<MessageHandlersRegisteredMarker>(_ => null);
            builder.ConfigureMessageHandlers(Configure);
        }

        private sealed class MessageHandlersRegisteredMarker { }

        private static void Configure(IMessageHandlerRegistry messageHandlerRegistry, IServiceProvider serviceProvider)
        {
            var options = serviceProvider.GetService<IOptions<MessagingOptions>>()?.Value ?? new MessagingOptions();
            var processors = options.MessageProcessors;
            var partManager = serviceProvider.GetRequiredService<ApplicationPartManager>();
            var messageHandlerFeature = new MessageHandlerFeature();

            partManager.PopulateFeature(messageHandlerFeature);

            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger("MessageHandlerRegistration");
            RegisterMessageHandlerTypes(messageHandlerFeature.MessageHandlers, messageHandlerRegistry, processors, logger);
        }

        private static void RegisterMessageHandlerTypes(
            IEnumerable<Type> types,
            IMessageHandlerRegistry messageHandlerRegistry,
            IList<IMessageProcessorRegistration> processors,
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
            IList<IMessageProcessorRegistration> processors,
            ILogger logger)
        {
            var memberDescriptors = MessageHandlerInspector.Instance.InspectType(handlerType);

            foreach (var memberDescriptor in memberDescriptors)
            {
                var registration = CreateMessageHandlerRegistration(memberDescriptor, processors);
                messageHandlerRegistry.Register(registration);

                logger?.LogDebug($"Registered handler of type '{handlerType}' for message-type '{memberDescriptor.MessageType}'.");
            }
        }

        private static IMessageHandlerRegistration CreateMessageHandlerRegistration(
            MessageHandlerActionDescriptor memberDescriptor,
            IList<IMessageProcessorRegistration> processors)
        {
            var configuration = memberDescriptor.BuildConfiguration();

            return new MessageHandlerRegistration(
                memberDescriptor.MessageType,
                configuration,
                serviceProvider => MessageHandlerInvoker.CreateInvoker(memberDescriptor, processors, serviceProvider),
                memberDescriptor);
        }


    }
}
