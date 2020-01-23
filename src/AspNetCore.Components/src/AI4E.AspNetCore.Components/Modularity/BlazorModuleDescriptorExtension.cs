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
using System.Reflection;

namespace AI4E.AspNetCore.Components.Modularity
{
    public static class BlazorModuleDescriptorExtension
    {
        public static BlazorModuleDescriptor.Builder ToBuilder(
            this IBlazorModuleDescriptor moduleDescriptor)
        {
            return new BlazorModuleDescriptor.Builder(moduleDescriptor);
        }

        public static IBlazorModuleDescriptor LoadAssembliesInContext(
            this IBlazorModuleDescriptor moduleDescriptor,
            params Assembly[] assemblies)
        {
            if (assemblies is null)
                throw new ArgumentNullException(nameof(assemblies));

            if (assemblies.Length == 0)
            {
                return moduleDescriptor;
            }

            var builder = moduleDescriptor.ToBuilder();

            foreach (var assembly in assemblies)
            {
                if (assembly is null)
                    throw new ArgumentException("The collection must not contain null entries.", nameof(assemblies));

                builder.LoadAssemblyInContext(assembly);
            }

            return builder.Build();
        }
    }
}
