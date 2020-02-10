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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Storage
{
    public static class ServiceCollectionExtension
    {
        public static IStorageBuilder AddStorage(this IServiceCollection services)
        {
            if (services is null)
                throw new NullReferenceException();

            if (!TryGetStorageBuilder(services, out var storageBuilder))
            {
                services.AddOptions();
                services.TryAddSingleton<IDatabase>(NoDatabase.Instance);

                storageBuilder = new StorageBuilderImpl(services);
                services.AddSingleton(storageBuilder);
            }

            return storageBuilder;
        }

        private static bool TryGetStorageBuilder(
            IServiceCollection services,
            [NotNullWhen(true)] out StorageBuilderImpl? storageBuilder)
        {
            storageBuilder = services.LastOrDefault(
                p => p.ServiceType == typeof(StorageBuilderImpl))?.ImplementationInstance as StorageBuilderImpl;

            return storageBuilder != null;
        }
    }
}
