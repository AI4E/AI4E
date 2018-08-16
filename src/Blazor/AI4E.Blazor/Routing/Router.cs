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
* Asp.Net Blazor
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
using AI4E.Blazor.ApplicationParts;
using AI4E.Blazor.Components;
using Microsoft.AspNetCore.Blazor.Components;
using Microsoft.AspNetCore.Blazor.Layouts;
using Microsoft.AspNetCore.Blazor.RenderTree;
using Microsoft.AspNetCore.Blazor.Services;
using BlazorInject = Microsoft.AspNetCore.Blazor.Components.InjectAttribute;

namespace AI4E.Blazor.Routing
{
    /// <summary>
    /// A component that displays whichever other component corresponds to the
    /// current navigation location.
    /// </summary>
    public class RouterX : IComponent, IDisposable
    {
        private static readonly char[] _queryOrHashStartChar = new[] { '?', '#' };

        private RenderHandle _renderHandle;
        private string _baseUri;
        private string _locationAbsolute;

        [BlazorInject] private IUriHelper UriHelper { get; set; }
        [BlazorInject] private ApplicationPartManager PartManager { get; set; }

        private RouteTable Routes { get; set; }

        /// <inheritdoc />
        public void Init(RenderHandle renderHandle)
        {
            _renderHandle = renderHandle;
            _baseUri = UriHelper.GetBaseUri();
            _locationAbsolute = UriHelper.GetAbsoluteUri();
            UriHelper.OnLocationChanged += OnLocationChanged;
            PartManager.ApplicationPartsChanged += OnApplicationPartsChanged;
        }

        /// <inheritdoc />
        public void SetParameters(ParameterCollection parameters)
        {
            parameters.AssignToProperties(this);
            UpdateRouteTable();
            Refresh();
        }

        private void UpdateRouteTable()
        {
            var componentFeature = new ComponentFeature();
            PartManager.PopulateFeature(componentFeature);

            var types = componentFeature.Components;
            Routes = RouteTable.Create(types);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            UriHelper.OnLocationChanged -= OnLocationChanged;
            PartManager.ApplicationPartsChanged -= OnApplicationPartsChanged;
        }

        private string StringUntilAny(string str, char[] chars)
        {
            var firstIndex = str.IndexOfAny(chars);
            return firstIndex < 0
                ? str
                : str.Substring(0, firstIndex);
        }

        /// <inheritdoc />
        protected virtual void Render(RenderTreeBuilder builder, Type handler, IDictionary<string, object> parameters)
        {
            builder.OpenComponent(0, typeof(LayoutDisplay));
            builder.AddAttribute(1, /*LayoutDisplay.NameOfPage*/"Page", handler); // TODO
            builder.AddAttribute(2, /*LayoutDisplay.NameOfPageParameters*/"PageParameters", parameters); // TODO
            builder.CloseComponent();
        }

        private void Refresh()
        {
            var locationPath = UriHelper.ToBaseRelativePath(_baseUri, _locationAbsolute);
            locationPath = StringUntilAny(locationPath, _queryOrHashStartChar);
            var context = new RouteContext(locationPath);
            Routes.Route(context);
            if (context.Handler == null)
            {
                throw new InvalidOperationException($"'{nameof(RouterX)}' cannot find any component with a route for '/{locationPath}'.");
            }

            if (!typeof(IComponent).IsAssignableFrom(context.Handler))
            {
                throw new InvalidOperationException($"The type {context.Handler.FullName} " +
                    $"does not implement {typeof(IComponent).FullName}.");
            }

            _renderHandle.Render(builder => Render(builder, context.Handler, context.Parameters));
        }

        private void OnLocationChanged(object sender, string newAbsoluteUri)
        {
            _locationAbsolute = newAbsoluteUri;
            if (_renderHandle.IsInitialized)
            {
                Refresh();
            }
        }

        private void OnApplicationPartsChanged(object sender, EventArgs e)
        {
            UpdateRouteTable();
        }
    }
}
