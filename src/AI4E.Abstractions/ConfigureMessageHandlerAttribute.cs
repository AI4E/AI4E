/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2019 Andreas Truetschel and contributors.
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

namespace AI4E
{
    /// <summary>
    /// Instructs the messaging system to apply the configuration that the attribute defined to the decorated message handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
    public abstract class ConfigureMessageHandlerAttribute : Attribute
    {
        /// <summary>
        /// Configures the message handler.
        /// </summary>
        /// <param name="memberDescriptor">The member descriptor that identifies the message handler.</param>
        /// <param name="configurationBuilder">The configuration builder.</param>
        protected abstract void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder);

        // TODO: This should be internal.
        public void ExecuteConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            if (configurationBuilder == null)
                throw new ArgumentNullException(nameof(configurationBuilder));

            ConfigureMessageHandler(memberDescriptor, configurationBuilder);
        }
    }
}
