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

namespace AI4E
{
    /// <summary>
    /// Configures a message handler to allow it as target for published messages only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class PublishOnlyAttribute : ConfigureMessageHandlerAttribute
    {
        /// <summary>
        /// Creates a new instance of type <see cref="PublishOnlyAttribute"/>.
        /// </summary>
        public PublishOnlyAttribute() : this(true) { }

        /// <summary>
        /// Creates a new instance of type <see cref="PublishOnlyAttribute"/>.
        /// </summary>
        /// <param name="publishOnly">A boolean value indicating whether the publish only constraint is applied.</param>
        public PublishOnlyAttribute(bool publishOnly)
        {
            PublishOnly = publishOnly;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the publish only constraint is applied.
        /// </summary>
        public bool PublishOnly { get; }

        /// <inheritdoc />
        protected override void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Configure(() => new PublishOnlyMessageHandlerConfiguration(PublishOnly));
        }
    }

    /// <summary>
    /// Represents a message handler feature indicating whether the publish only constraint is applied.
    /// </summary>
    public sealed class PublishOnlyMessageHandlerConfiguration : IMessageHandlerConfigurationFeature
    {
        /// <summary>
        /// Creates a new instance of type <see cref="PublishOnlyMessageHandlerConfiguration"/>.
        /// </summary>
        /// <param name="publishOnly">A boolean value indicating whether the publish only constraint is applied.</param>
        public PublishOnlyMessageHandlerConfiguration(bool publishOnly)
        {
            PublishOnly = publishOnly;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the publish only constraint is applied.
        /// </summary>
        public bool PublishOnly { get; }

        bool IMessageHandlerConfigurationFeature.IsEnabled => PublishOnly;
    }
}
