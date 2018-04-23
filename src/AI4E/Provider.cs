/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E
{
    public sealed class Provider<T> : IProvider<T>
    {
        private readonly Func<T> _factory;

        public Provider(Func<T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
        }

        public static Provider<T> FromValue(T value)
        {
            return new Provider<T>(() => value);
        }

        public T ProvideInstance()
        {
            return _factory();
        }

        public static implicit operator Provider<T>(Func<T> factory)
        {
            return new Provider<T>(factory);
        }
    }

    public static class Provider
    {
        public static IProvider<T> Create<T>(IServiceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            return new Provider<T>(() => ActivatorUtilities.CreateInstance<T>(provider));
        }

        public static IProvider<T> FromServices<T>(IServiceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            return new Provider<T>(() => provider.GetRequiredService<T>());
        }

        public static IProvider<T> Create<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new Provider<T>(() => value);
        }

        public static IProvider<T> Create<T>(Func<T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return new Provider<T>(() => factory());
        }
    }
}
