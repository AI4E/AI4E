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
using AI4E.ApplicationParts;
using Microsoft.AspNetCore.Blazor;
using Microsoft.AspNetCore.Blazor.Components;
using Microsoft.AspNetCore.Blazor.RenderTree;
using BlazorInject = Microsoft.AspNetCore.Blazor.Components.InjectAttribute;

namespace AI4E.Blazor.Components
{
    internal sealed class ViewExtension<TViewExtension> : IComponent, IDisposable
    {
        internal static readonly string ConfigurationName = nameof(Configuration);

        private readonly List<Type> _viewExtensions = new List<Type>();
        private readonly RenderFragment _renderFragment;
        private RenderHandle _renderHandle;
        private bool _hasCalledInit;

        /// <summary>
        /// Constructs an instance of <see cref="BlazorComponent"/>.
        /// </summary>
        public ViewExtension()
        {
            _renderFragment = BuildRenderTree;
        }

        [Parameter] private Action<TViewExtension> Configuration { get; set; }
        [BlazorInject] private ApplicationPartManager PartManager { get; set; }

        public void Init(RenderHandle renderHandle)
        {
            // This implicitly means a BlazorComponent can only be associated with a single
            // renderer. That's the only use case we have right now. If there was ever a need,
            // a component could hold a collection of render handles.
            if (_renderHandle.IsInitialized)
            {
                throw new InvalidOperationException($"The render handle is already set. Cannot initialize a {nameof(BlazorComponent)} more than once.");
            }

            _renderHandle = renderHandle;
        }

        /// <summary>
        /// Method invoked to apply initial or updated parameters to the component.
        /// </summary>
        /// <param name="parameters">The parameters to apply.</param>
        public void SetParameters(ParameterCollection parameters)
        {
            parameters.AssignToProperties(this);

            if (!_hasCalledInit)
            {
                _hasCalledInit = true;
                PartManager.ApplicationPartsChanged += ApplicationPartsChanged;
                UpdateViewExtensions();
            }

            StateHasChanged();
        }

        private void ApplicationPartsChanged(object sender, EventArgs e)
        {
            UpdateViewExtensions();
            StateHasChanged();
        }

        private void UpdateViewExtensions()
        {
            var feature = new ViewExtensionFeature(typeof(TViewExtension));
            PartManager.PopulateFeature(feature);

            _viewExtensions.Clear();
            _viewExtensions.AddRange(feature.ViewExtensions);
        }

        /// <summary>
        /// Notifies the component that its state has changed. When applicable, this will
        /// cause the component to be re-rendered.
        /// </summary>
        private void StateHasChanged()
        {
            _renderHandle.Render(_renderFragment);
        }

        /// <summary>
        /// Renders the component to the supplied <see cref="RenderTreeBuilder"/>.
        /// </summary>
        /// <param name="builder">A <see cref="RenderTreeBuilder"/> that will receive the render output.</param>
        private void BuildRenderTree(RenderTreeBuilder builder)
        {
            var components = _viewExtensions;
            foreach (var component in components)
            {
                builder.OpenComponent(0, typeof(ViewExtensionWrapper<>).MakeGenericType(component));
                builder.AddAttribute(0, ConfigurationName, Configuration);
                builder.CloseComponent();
            }
        }

        public void Dispose()
        {
            if (PartManager != null)
            {
                PartManager.ApplicationPartsChanged -= ApplicationPartsChanged;
            }
        }
    }

    internal sealed class ViewExtensionWrapper<TViewExtension> : IComponent
        where TViewExtension : IComponent
    {
        private RenderHandle _renderHandle;
        private bool _hasCalledInit;
        private TViewExtension _component;

        [BlazorInject] private IServiceProvider ServiceProvider { get; set; }
        [Parameter] private Action<TViewExtension> Configuration { get; set; }

        public void Init(RenderHandle renderHandle)
        {
            // This implicitly means a BlazorComponent can only be associated with a single
            // renderer. That's the only use case we have right now. If there was ever a need,
            // a component could hold a collection of render handles.
            if (_renderHandle.IsInitialized)
            {
                throw new InvalidOperationException($"The render handle is already set. Cannot initialize a {nameof(BlazorComponent)} more than once.");
            }

            _renderHandle = renderHandle;
        }

        public void SetParameters(ParameterCollection parameters)
        {
            parameters.AssignToProperties(this);

            if (!_hasCalledInit)
            {
                _hasCalledInit = true;
                var componentFactory = new ComponentFactory(ServiceProvider);

                _component = (TViewExtension)componentFactory.InstantiateComponent(typeof(TViewExtension));
                _component.Init(_renderHandle);
                Configuration?.Invoke(_component);
            }

            _component.SetParameters(ParameterCollection.Empty);
        }
    }
}
