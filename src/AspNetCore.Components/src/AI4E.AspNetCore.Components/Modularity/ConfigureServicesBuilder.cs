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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * ASP.Net Core (https://github.com/aspnet/AspNetCore)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal class ConfigureServicesBuilder
    {
        public ConfigureServicesBuilder(MethodInfo? configureServices)
        {
            MethodInfo = configureServices;
        }

        public MethodInfo? MethodInfo { get; }

        public Func<Func<IServiceCollection, IServiceProvider?>, Func<IServiceCollection, IServiceProvider?>> StartupServiceFilters { get; set; } = f => f;

        public Func<IServiceCollection, IServiceProvider?> Build(object? instance)
        {
            return services => Invoke(instance, services);
        }

        private IServiceProvider? Invoke(object? instance, IServiceCollection services)
        {
            return StartupServiceFilters(Startup)(services);

            IServiceProvider? Startup(IServiceCollection serviceCollection)
            {
                return InvokeCore(instance, serviceCollection);
            }
        }

        private IServiceProvider? InvokeCore(object? instance, IServiceCollection services)
        {
            if (MethodInfo == null)
            {
                return null;
            }

            // Only support IServiceCollection parameters
            var parameters = MethodInfo.GetParameters();
            if (parameters.Length > 1 ||
                parameters.Any(p => p.ParameterType != typeof(IServiceCollection)))
            {
                throw new InvalidOperationException(
                    "The ConfigureServices method must either be parameterless or take only one parameter of type IServiceCollection.");
            }

            var arguments = new object[MethodInfo.GetParameters().Length];

            if (parameters.Length > 0)
            {
                arguments[0] = services;
            }

            return MethodInfo.InvokeWithoutWrappingExceptions(instance, arguments) as IServiceProvider;
        }
    }
}
