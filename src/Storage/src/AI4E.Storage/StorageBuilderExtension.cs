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

namespace AI4E.Storage
{
    public static class StorageBuilderExtension
    {
        public static IStorageBuilder UseDatabase<TDatabase>(this IStorageBuilder builder)
            where TDatabase : class, IDatabase
        {
#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

            services.AddSingleton<IDatabase, TDatabase>();

            return builder;
        }

        public static IStorageBuilder UseDatabase<TDatabase>(this IStorageBuilder builder, Func<IServiceProvider, TDatabase> factory)
            where TDatabase : class, IDatabase
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

            services.AddSingleton<IDatabase, TDatabase>(factory);

            return builder;
        }   
    }
}
