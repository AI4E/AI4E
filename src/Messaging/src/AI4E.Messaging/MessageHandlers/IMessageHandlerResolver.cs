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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace AI4E.Messaging.MessageHandlers
{
    public interface IMessageHandlerResolver
    {
        public IEnumerable<Type> ResolveMessageHandlers(IAssemblySource assemblySource)
        {
            if (assemblySource is null)
                throw new ArgumentNullException(nameof(assemblySource));

            // By reference equality comparison is ok for this case.
            var resultBuilder = ImmutableHashSet.CreateBuilder<Type>();

            foreach (var assembly in assemblySource.Assemblies)
            {
                var loadContext = assemblySource.GetAssemblyLoadContext(assembly);
                resultBuilder.UnionWith(ResolveMessageHandlers(assembly, loadContext));
            }

            return resultBuilder.ToImmutable();
        }

        [Obsolete]
        public IEnumerable<Type> ResolveMessageHandlers(
            IEnumerable<Assembly> assemblies, AssemblyLoadContext? loadContext = null)
        {
            if (assemblies is null)
                throw new ArgumentNullException(nameof(assemblies));

            return assemblies.SelectMany(
                assembly => ResolveMessageHandlers(assembly, loadContext)).ToImmutableHashSet();
        }

        public IEnumerable<Type> ResolveMessageHandlers(Assembly assembly, AssemblyLoadContext? loadContext = null);
    }
}
