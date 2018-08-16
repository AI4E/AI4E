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
using AI4E.Blazor.ApplicationParts;
using Microsoft.AspNetCore.Blazor.Components;

namespace AI4E.Blazor.Components
{
    public sealed class ComponentFeature
    {
        public IList<Type> Components { get; } = new List<Type>();
    }

    public sealed class ComponentFeatureProvider : IApplicationFeatureProvider<ComponentFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ComponentFeature feature)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));

            if (feature == null)
                throw new ArgumentNullException(nameof(feature));

            foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
            {
                foreach (var type in part.Types)
                {
                    if (IsComponent(type) && !feature.Components.Contains(type))
                    {
                        feature.Components.Add(type);
                    }
                }
            }
        }

        internal static bool IsComponent(Type type)
        {
            if (!type.IsClass)
                return false;

            if (type.IsAbstract)
                return false;

            if (!type.IsPublic)
                return false;

            if (type.ContainsGenericParameters)
                return false;

            return typeof(IComponent).IsAssignableFrom(type);
        }
    }
}
