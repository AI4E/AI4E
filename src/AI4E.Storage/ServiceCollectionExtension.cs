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
using AI4E.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage
{
    public static class ServiceCollectionExtension
    {
        public static IStorageBuilder AddStorage(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddOptions();
            services.AddCoreServices();
            services.AddSingleton<IMessageAccessor, DefaultMessageAccessor>();
            services.AddSingleton<ISerializer>(new Serialization.JsonSerializer());

            return new StorageBuilder(services);
        }

        public static IStorageBuilder AddStorage(this IServiceCollection services, Action<StorageOptions> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var builder = services.AddStorage();
            builder.Configure(configuration);

            return builder;
        }
    }
}
