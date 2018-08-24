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

/* Based on
* --------------------------------------------------------------------------------------------------------------------
* Asp.Net Core MVC
* Copyright (c) .NET Foundation. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use
* these files except in compliance with the License. You may obtain a copy of the
* License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed
* under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
* CONDITIONS OF ANY KIND, either express or implied. See the License for the
* specific language governing permissions and limitations under the License.
* --------------------------------------------------------------------------------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.Reflection;

namespace AI4E.ApplicationParts
{
    /// <summary>
    /// Specifies a contract for synthesizing one or more <see cref="ApplicationPart"/> instances
    /// from an <see cref="Assembly"/>.
    /// <para>
    /// By default, Mvc registers each application assembly that it discovers as an <see cref="AssemblyPart"/>.
    /// Assemblies can optionally specify an <see cref="ApplicationPartFactory"/> to configure parts for the assembly
    /// by using <see cref="ProvideApplicationPartFactoryAttribute"/>.
    /// </para>
    /// </summary>
    public abstract class ApplicationPartFactory
    {
        /// <summary>
        /// Gets one or more <see cref="ApplicationPart"/> instances for the specified <paramref name="assembly"/>.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/>.</param>
        public abstract IEnumerable<ApplicationPart> GetApplicationParts(Assembly assembly);

        /// <summary>
        /// Gets the <see cref="ApplicationPartFactory"/> for the specified assembly.
        /// <para>
        /// An assembly may specify an <see cref="ApplicationPartFactory"/> using <see cref="ProvideApplicationPartFactoryAttribute"/>.
        /// Otherwise, <see cref="DefaultApplicationPartFactory"/> is used.
        /// </para>
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/>.</param>
        /// <returns>An instance of <see cref="ApplicationPartFactory"/>.</returns>
        public static ApplicationPartFactory GetApplicationPartFactory(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var provideAttribute = assembly.GetCustomAttribute<ProvideApplicationPartFactoryAttribute>();
            if (provideAttribute == null)
            {
                return DefaultApplicationPartFactory.Instance;
            }

            var type = provideAttribute.GetFactoryType();
            if (!typeof(ApplicationPartFactory).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(string.Format(
                    "Type {0} specified by {1} is invalid. Type specified by {1} must derive from {2}.",
                    type,
                    nameof(ProvideApplicationPartFactoryAttribute),
                    typeof(ApplicationPartFactory)));
            }

            return (ApplicationPartFactory)Activator.CreateInstance(type);
        }
    }
}
