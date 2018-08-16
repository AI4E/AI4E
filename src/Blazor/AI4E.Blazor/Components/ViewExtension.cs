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
using AI4E.Blazor.ApplicationParts;
using Microsoft.AspNetCore.Blazor.Components;
using Microsoft.AspNetCore.Blazor.RenderTree;
using BlazorInject = Microsoft.AspNetCore.Blazor.Components.InjectAttribute;

namespace AI4E.Blazor.Components
{
    public sealed class ViewExtension : IComponent, IDisposable
    {
        private RenderHandle _renderHandle;

        [BlazorInject] private ApplicationPartManager PartManager { get; set; }
        [Parameter] private Type Type { get; set; }

        public void Init(RenderHandle renderHandle)
        {
            _renderHandle = renderHandle;
            PartManager.ApplicationPartsChanged += OnApplicationPartsChanged;
        }

        public void SetParameters(ParameterCollection parameters)
        {
            parameters.AssignToProperties(this);

            _renderHandle.Render(builder => Render(builder));
        }


        public void Dispose()
        {
            PartManager.ApplicationPartsChanged -= OnApplicationPartsChanged;
        }

        private void OnApplicationPartsChanged(object sender, EventArgs e)
        {
            _renderHandle.Render(builder => Render(builder));
        }

        private void Render(RenderTreeBuilder builder)
        {
            if (Type == null)
                return; // TODO: throw

            var feature = new ViewExtensionFeature(Type);
            PartManager.PopulateFeature(feature);

            foreach (var viewExtension in feature.ViewExtensions)
            {
                builder.OpenComponent(0, viewExtension);
                // TODO: Arguments
                builder.CloseComponent();
            }
        }
    }
}
