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
    /// Represents a builder that can be used to construct <see cref="MessageHandlerConfiguration"/>s.
    /// </summary>
    public interface IMessageHandlerConfigurationBuilder
    {
        /// <summary>
        /// Configures the message handler configuration.
        /// </summary>
        /// <typeparam name="TConfig">The configuration type.</typeparam>
        /// <param name="configuration">The message handler configuration.</param>
        /// <returns>The configuration builder with the configuration applied.</returns>
        IMessageHandlerConfigurationBuilder Configure<TConfig>(
            Func<TConfig, TConfig> configuration)
            where TConfig : class;

        /// <summary>
        /// Configures the message handler configuration.
        /// </summary>
        /// <typeparam name="TConfig">The configuration type.</typeparam>
        /// <param name="configuration">The message handler configuration.</param>
        /// <returns>The configuration builder with the configuration applied.</returns>
        IMessageHandlerConfigurationBuilder Configure<TConfig>(
           Func<TConfig> configuration)
           where TConfig : class;

        /// <summary>
        /// Configures the message handler configuration.
        /// </summary>
        /// <typeparam name="TConfig">The configuration type.</typeparam>
        /// <param name="configuration">The message handler configuration.</param>
        /// <returns>The configuration builder with the configuration applied.</returns>
        IMessageHandlerConfigurationBuilder Configure<TConfig>(
            Action<TConfig> configuration)
            where TConfig : class, new();

        /// <summary>
        /// Builds the message handler configuration.
        /// </summary>
        /// <returns>The built <see cref="MessageHandlerConfiguration"/>.</returns>
        MessageHandlerConfiguration Build();
    }
}
