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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Utils.DependencyInjection
{
    public sealed class WeakServiceProvider : IServiceProvider, ISupportRequiredService
    {
        private readonly WeakReference<IServiceProvider> _serviceProvider;

        public WeakServiceProvider(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = new WeakReference<IServiceProvider>(serviceProvider);
        }

        public object? GetService(Type serviceType)
        {
            if (_serviceProvider.TryGetTarget(out var serviceProvider))
            {
                return serviceProvider.GetService(serviceType);
            }

            return null;
        }

        public object GetRequiredService(Type serviceType)
        {
            if (_serviceProvider.TryGetTarget(out var serviceProvider))
            {
                if (serviceProvider is ISupportRequiredService requiredServiceSupporter)
                {
                    return requiredServiceSupporter.GetRequiredService(serviceType);
                }

                return serviceProvider.GetRequiredService(serviceType);
            }

            throw new InvalidOperationException();
        }
    }
}
