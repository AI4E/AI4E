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
using AI4E.Utils.ApplicationParts;

namespace AI4E.Blazor.Components
{
    public sealed class ViewExtensionFeature
    {
        public ViewExtensionFeature(Type viewExtensionType)
        {
            if (viewExtensionType == null)
                throw new ArgumentNullException(nameof(viewExtensionType));

            ViewExtensionType = viewExtensionType;
        }

        public Type ViewExtensionType { get; }

        public IList<Type> ViewExtensions { get; } = new List<Type>();
    }

    public sealed class ViewExtensionFeatureProvider : IApplicationFeatureProvider<ViewExtensionFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ViewExtensionFeature feature)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));

            if (feature == null)
                throw new ArgumentNullException(nameof(feature));

            foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
            {
                foreach (var type in part.Types)
                {
                    if (ComponentFeatureProvider.IsComponent(type) &&
                        IsViewExtension(type, feature.ViewExtensionType) &&
                        !feature.ViewExtensions.Contains(type))
                    {
                        feature.ViewExtensions.Add(type);
                    }
                }
            }
        }

        private static bool IsViewExtension(Type type, Type viewExtensionType)
        {
            return viewExtensionType.IsAssignableFrom(type);
        }
    }
}
