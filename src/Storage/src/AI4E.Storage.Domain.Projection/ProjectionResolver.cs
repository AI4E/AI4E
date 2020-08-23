/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using AI4E.Utils;
using Microsoft.Extensions.Options;

namespace AI4E.Storage.Domain.Projection
{
    public sealed class ProjectionResolver : IProjectionResolver
    {
        private static readonly Func<Type, bool> _isProjection
           = new Func<Type, bool>(IsProjection);

        private readonly IOptions<DomainProjectionOptions> _optionsProvider;

        public ProjectionResolver(IOptions<DomainProjectionOptions> optionsProvider)
        {
            if (optionsProvider is null)
                throw new ArgumentNullException(nameof(optionsProvider));

            _optionsProvider = optionsProvider;
        }

        public IEnumerable<Type> ResolveProjections(Assembly assembly, AssemblyLoadContext? loadContext = null)
        {
            var assemblyResolver = new AssemblyResolver(
                assembly.GetName(),
                _optionsProvider.Value.IncludeAssemblyDependencies,
                _optionsProvider.Value.ExcludedAssemblies);

            // Default comparison is ok for this case.
            var resultsBuilder = ImmutableHashSet.CreateBuilder<Type>();

            foreach (var resolvedAssembly in assemblyResolver.GetAssemblies(assembly, loadContext))
            {
                resultsBuilder.UnionWith(GetProjections(resolvedAssembly));
            }

            return resultsBuilder.ToImmutable();
        }

        public static IEnumerable<Type> GetProjections(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            return assembly.DefinedTypes.Where(_isProjection);
        }

        private static bool IsProjection(Type type, bool allowAbstract)
        {
            if (type.IsInterface || type.IsEnum)
                return false;

            if (!allowAbstract && type.IsAbstract)
                return false;

            if (type.ContainsGenericParameters)
                return false;

            if (type.IsDefined<NoProjectionAttribute>(inherit: false))
                return false;

            if (type.Name.EndsWith("Projection", StringComparison.OrdinalIgnoreCase) && type.IsPublic)
                return true;

            if (type.IsDefined<ProjectionAttribute>(inherit: false))
                return true;

            return type.BaseType != null && IsProjection(type.BaseType, allowAbstract: true);
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified type is a projection.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if <paramref name="type"/> is a projection, false otherwise.</returns>
        public static bool IsProjection(Type type)
        {
            return IsProjection(type, allowAbstract: false);
        }
    }
}
