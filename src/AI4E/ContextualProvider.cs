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
    [Obsolete]
    public sealed class ContextualProvider<T> : IContextualProvider<T>
    {
        private readonly Func<IServiceProvider, T> _factory;

        public ContextualProvider(Func<IServiceProvider, T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
        }

        private static Lazy<ContextualProvider<T>> _fromContext = new Lazy<ContextualProvider<T>>(
            () => new ContextualProvider<T>(provider => ActivatorUtilities.CreateInstance<T>(provider)),
            isThreadSafe: true);

        public static ContextualProvider<T> FromContext => _fromContext.Value;

        public static ContextualProvider<T> FromValue(T value)
        {
            return new ContextualProvider<T>(provider => value);
        }

        public T ProvideInstance(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return _factory(serviceProvider);
        }

        public static implicit operator ContextualProvider<T>(Func<IServiceProvider, T> factory)
        {
            return new ContextualProvider<T>(factory);
        }
    }

    [Obsolete]
    public static class ContextualProvider
    {
        public static ContextualProvider<T> Create<T>()
        {
            return new ContextualProvider<T>(provider => ActivatorUtilities.CreateInstance<T>(provider));
        }

        public static ContextualProvider<T> Create<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new ContextualProvider<T>(provider => value);
        }

        public static ContextualProvider<T> Create<T>(Func<T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return new ContextualProvider<T>(_ => factory());
        }

        public static ContextualProvider<T> Create<T>(Func<IServiceProvider, T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return new ContextualProvider<T>(factory);
        }
    }
}
