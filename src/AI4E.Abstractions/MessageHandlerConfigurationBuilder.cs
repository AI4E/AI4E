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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using AI4E.Utils;

namespace AI4E
{
    /// <summary>
    /// Represents a builder that can be used to construct <see cref="MessageHandlerConfiguration"/>s.
    /// </summary>
    public sealed class MessageHandlerConfigurationBuilder : IMessageHandlerConfigurationBuilder
    {
        private readonly ImmutableDictionary<Type, object>.Builder _dataBuilder;

        /// <summary>
        /// Creates a new instance of type <see cref="MessageHandlerConfigurationBuilder"/>.
        /// </summary>
        public MessageHandlerConfigurationBuilder()
        {
            _dataBuilder = ImmutableDictionary.CreateBuilder<Type, object>();
        }

        internal MessageHandlerConfigurationBuilder(IEnumerable<KeyValuePair<Type, object>> existing)
        {
            _dataBuilder = ImmutableDictionary.CreateBuilder<Type, object>();
            foreach (var kvp in existing)
            {
                _dataBuilder[kvp.Key] = kvp.Value;
            }
        }

        private IMessageHandlerConfigurationBuilder Configure(
            Type configurationType,
            Func<object, object> configuration)
        {
            if (configurationType == null)
                throw new ArgumentNullException(nameof(configurationType));

            if (!configurationType.IsOrdinaryClass())
                throw new ArgumentException("The specified type must be an ordinary reference type.", nameof(configurationType));

            if (_dataBuilder.TryGetValue(configurationType, out var obj))
            {
                Debug.Assert(configurationType.IsAssignableFrom(obj.GetType()));
            }

            obj = configuration(obj);

            if (obj == null)
            {
                _dataBuilder.Remove(configurationType);
            }
            else
            {
                if (!configurationType.IsAssignableFrom(obj.GetType()))
                {
                    throw new InvalidOperationException();
                }

                _dataBuilder[configurationType] = obj;
            }

            return this;
        }

        /// <inheritdoc />
        public IMessageHandlerConfigurationBuilder Configure<TConfig>(
            Func<TConfig, TConfig> configuration)
            where TConfig : class
        {
            return Configure(typeof(TConfig), o => configuration(o as TConfig));
        }

        /// <inheritdoc />
        public IMessageHandlerConfigurationBuilder Configure<TConfig>(
            Func<TConfig> configuration)
            where TConfig : class
        {
            return Configure(typeof(TConfig), o => configuration());
        }

        /// <inheritdoc />
        public IMessageHandlerConfigurationBuilder Configure<TConfig>(
            Action<TConfig> configuration)
            where TConfig : class, new()
        {
            object Configuration(object obj)
            {
                var config = obj as TConfig ?? new TConfig();
                configuration(config);

                return config;
            }

            return Configure(typeof(TConfig), Configuration);
        }

        /// <inheritdoc />
        public MessageHandlerConfiguration Build()
        {
            var data = _dataBuilder.ToImmutable();
            return new MessageHandlerConfiguration(data);
        }
    }
}
