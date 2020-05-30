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
using System.Linq;
using AI4E.Utils.ApplicationParts;

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Represents a projection application feature.
    /// </summary>
    public class ProjectionFeature
    {
        /// <summary>
        /// Gets the list of types that declare projections.
        /// </summary>
        public IList<Type> Projections { get; } = new List<Type>();
    }

    /// <summary>
    /// Represents a projection feature provider.
    /// </summary>
    public class ProjectionFeatureProvider : IApplicationFeatureProvider<ProjectionFeature>
    {
        /// <inheritdoc/>
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ProjectionFeature feature)
        {
            foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
            {
                foreach (var type in part.Types)
                {
                    if (IsProjection(type) && !feature.Projections.Contains(type))
                    {
                        feature.Projections.Add(type);
                    }
                }
            }
        }

        private bool IsProjection(Type type, bool allowAbstract)
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
        protected internal virtual bool IsProjection(Type type)
        {
            return IsProjection(type, allowAbstract: false);
        }

        internal static void Configure(ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<ProjectionFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new ProjectionFeatureProvider());
            }
        }
    }
}
