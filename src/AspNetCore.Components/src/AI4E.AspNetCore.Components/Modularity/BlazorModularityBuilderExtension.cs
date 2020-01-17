﻿/* License
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
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.AspNetCore.Components.Modularity
{
    public static class BlazorModularityBuilderExtension
    {
        public static IBlazorModularityBuilder Configure(
            this IBlazorModularityBuilder builder,
            Action<BlazorModuleOptions> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CA1062
            builder.Services.Configure(configuration);
#pragma warning restore CA1062

            return builder;
        }

        public static IBlazorModularityBuilder UseDefaultModuleSource(this IBlazorModularityBuilder builder)
        {
#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

            services.AddSingleton<IBlazorModuleSource, BlazorModuleSource>();
            services.TryAddSingleton<IBlazorModuleAssemblyLoader, BlazorModuleAssemblyLoader>();

            return builder;
        }
    }
}