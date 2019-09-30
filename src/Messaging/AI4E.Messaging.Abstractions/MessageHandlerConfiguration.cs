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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AI4E.Messaging
{
    /// <summary>
    /// Represents the configuration of a message handler.
    /// </summary>
    public readonly struct MessageHandlerConfiguration
    {
        private readonly ImmutableDictionary<Type, object> _data;

        public MessageHandlerConfiguration(ImmutableDictionary<Type, object> data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            _data = data;
        }

        private bool TryGetConfiguration(Type configurationType, out object configuration)
        {
            Debug.Assert(configurationType != null);

            if (!configurationType.IsOrdinaryClass())
                throw new ArgumentException("The specified type must be an ordinary reference type.", nameof(configurationType));

            if (_data == null)
            {
                configuration = null;
                return false;
            }

            if (!_data.TryGetValue(configurationType, out configuration))
            {
                configuration = null;
                return false;
            }

            if (configuration == null)
            {
                return false;
            }

            if (!configurationType.IsAssignableFrom(configuration.GetType()))
            {
                configuration = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to retrieve the configuration of the specified type.
        /// </summary>
        /// <typeparam name="TConfig">The type of configuration.</typeparam>
        /// <param name="configuration">Contains the configuration if the operation suceeds, <c>null</c> otherwise.</param>
        /// <returns>True, if the operation is successful, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="TConfig"/> is not an ordinary reference type.</exception>
        public bool TryGetConfiguration<TConfig>(
            out TConfig configuration)
            where TConfig : class
        {
            configuration = default;
            return TryGetConfiguration(typeof(TConfig), out Unsafe.As<TConfig, object>(ref configuration));
        }

        /// <summary>
        /// Retrieves the configuration of the specified type.
        /// </summary>
        /// <typeparam name="TConfig">The type of configuration.</typeparam>
        /// <returns>The configuration or a new instance if the configuration was not found.</returns>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="TConfig"/> is not an ordinary reference type.</exception>
        public TConfig GetConfiguration<TConfig>()
            where TConfig : class, new()
        {
            if (!TryGetConfiguration<TConfig>(out var config))
            {
                config = new TConfig();
            }

            return config;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the feature of the specified type is enabled.
        /// </summary>
        /// <typeparam name="TFeature">The type of feature.</typeparam>
        /// <param name="defaultValue">The default value if the feature is not found.</param>
        /// <returns>
        /// A boolean value indicating whether the feature specified by <typeparamref name="TFeature"/> is enabled,
        /// or <paramref name="defaultValue"/> if the feature was not found.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="TFeature"/> is not an ordinary reference type.</exception>
        public bool IsEnabled<TFeature>(bool defaultValue = false)
             where TFeature : class, IMessageHandlerConfigurationFeature
        {
            if (!TryGetConfiguration<TFeature>(out var config))
            {
                return defaultValue;
            }

            return config.IsEnabled;
        }
    }
}
