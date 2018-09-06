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
using System.Collections.Generic;
using System.Linq;
using AI4E.ApplicationParts;
using AI4E.Internal;

namespace AI4E.Storage.Projection
{
    public class ProjectionFeature
    {
        public IList<Type> Projections { get; } = new List<Type>();
    }

    public sealed class ProjectionFeatureProvider : IApplicationFeatureProvider<ProjectionFeature>
    {
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

        private bool IsProjection(Type type)
        {
            return (type.IsClass || type.IsValueType && !type.IsEnum) &&
                   !type.IsAbstract &&
                   type.IsPublic &&
                   !type.ContainsGenericParameters &&
                   !type.IsDefined<NoProjectionAttribute>() &&
                   (type.Name.EndsWith("Projection", StringComparison.OrdinalIgnoreCase) || type.IsDefined<ProjectionAttribute>());
        }
    }
}
