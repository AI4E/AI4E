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

using System.Reflection;

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extensions for <see cref="MessageHandlerActionDescriptor"/>s.
    /// </summary>
    public static class MessageHandlerActionDescriptorExtension
    {
        /// <summary>
        /// Builds the the message handler configuration of the specified message handler.
        /// </summary>
        /// <param name="memberDescriptor">The <see cref="MessageHandlerActionDescriptor"/> that identified the message handler.</param>
        /// <returns>The built message handler configuration.</returns>
        public static MessageHandlerConfiguration BuildConfiguration(this in MessageHandlerActionDescriptor memberDescriptor)
        {
            var configurationBuilder = new MessageHandlerConfigurationBuilder();

            // Process every attributes on the handlers assembly.
            foreach (var typeConfigAttribute in memberDescriptor.MessageHandlerType.Assembly.GetCustomAttributes<ConfigureMessageHandlerAttribute>(inherit: true))
            {
                typeConfigAttribute.Configure(memberDescriptor, configurationBuilder);
            }

            // Process every attributes on the handler type.
            foreach (var typeConfigAttribute in memberDescriptor.MessageHandlerType.GetCustomAttributes<ConfigureMessageHandlerAttribute>(inherit: true))
            {
                typeConfigAttribute.Configure(memberDescriptor, configurationBuilder);
            }

            // Process every attributes on the handler member.
            foreach (var actionConfigAttribute in memberDescriptor.Member.GetCustomAttributes<ConfigureMessageHandlerAttribute>(inherit: true))
            {
                actionConfigAttribute.Configure(memberDescriptor, configurationBuilder);
            }

            return configurationBuilder.Build();
        }
    }
}
