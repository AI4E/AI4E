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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AI4E.AspNetCore.Components.Extensibility
{
    /// <summary>
    /// A placeholder for view extensions, that renders all available view extensions.
    /// </summary>
    /// <typeparam name="TViewExtension">The type of view extension definition.</typeparam>
    public sealed class ViewExtensionPlaceholder<TViewExtension> : IComponent, IDisposable
        where TViewExtension : IViewExtensionDefinition
    {
        private readonly RenderFragment _renderFragment;  // Cache to avoid per-render allocations

        private RenderHandle _renderHandle;
        private bool _isInit;
        private readonly HashSet<Type> _viewExtensions = new HashSet<Type>();
        private ImmutableList<Assembly> _viewExtensionsAssemblies = ImmutableList<Assembly>.Empty;
        private ImmutableList<Assembly> _previousViewExtensionsAssemblies = ImmutableList<Assembly>.Empty;

        private IAssemblySource? _assemblySource;

        private IAssemblySource AssemblySource => _assemblySource ??= AssemblyRegistry.AssemblySource;

        /// <summary>
        /// Creates a new instance of the <see cref="ViewExtensionPlaceholder{TViewExtension}"/> type.
        /// </summary>
        public ViewExtensionPlaceholder()
        {
            _renderFragment = Render;
        }

        [Inject] private IAssemblyRegistry AssemblyRegistry { get; set; } = null!;

        /// <summary>
        /// Gets or sets the view-extension context.
        /// </summary>
        [Parameter] public object? Context { get; set; }

        /// <summary>
        /// Gets or sets a collection of attributes that will be applied to the rendered view-extension.
        /// </summary>
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? ViewExtensionAttributes { get; set; }

        /// <inheritdoc />
        public void Attach(RenderHandle renderHandle)
        {
            if (_renderHandle.IsInitialized)
            {
                throw new InvalidOperationException("Cannot set the render handler to the component multiple times.");
            }

            _renderHandle = renderHandle;
        }

        /// <inheritdoc />
        public Task SetParametersAsync(ParameterView parameters)
        {
            parameters.SetParameterProperties(this);

            if (!_isInit)
            {
                _isInit = true;

                Init();
            }

            Refresh(force: true);
            return Task.CompletedTask;
        }

        private void Init()
        {
            AssemblyRegistry.AssemblySourceChanged += AssemblySourceChanged;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            AssemblyRegistry.AssemblySourceChanged -= AssemblySourceChanged;
        }

        private void AssemblySourceChanged(object? sender, EventArgs args)
        {
            _ = _renderHandle.Dispatcher.InvokeAsync(() =>
            {
                _assemblySource = null;
                Refresh(force: false);

                if (UpdateNeeded())
                {
                    Refresh(force: true);
                }
            });
        }

        private bool UpdateNeeded()
        {
            return _previousViewExtensionsAssemblies.Any(assembly => !AssemblySource.ContainsAssembly(assembly));
        }

        // We need to store the cached values in a weak-table to allow assemblies to be unloaded.
        private static readonly ConditionalWeakTable<Assembly, ImmutableList<Type>> ViewExtensionsLookup
            = new ConditionalWeakTable<Assembly, ImmutableList<Type>>();

        private static ImmutableList<Type> GetViewExtensions(Assembly assembly)
        {
            return ViewExtensionsLookup.GetValue(assembly, GetViewExtensionsUncached);
        }

        private static ImmutableList<Type> GetViewExtensionsUncached(Assembly assembly)
        {
            return assembly.ExportedTypes.Where(IsViewExtension).ToImmutableList();
        }

        private static bool IsViewExtension(Type type)
        {
            if (!typeof(TViewExtension).IsAssignableFrom(type))
                return false;

            if (type.IsInterface)
                return false;

            if (type.IsAbstract)
                return false;

            // The view-extension definition itself is not a view-extension we may consider,
            // otherwise we end up in an infinite loop.
            return type != typeof(TViewExtension);
        }

        private void Refresh(bool force)
        {
            var assemblies = AssemblySource.Assemblies;
            var viewExtensions = assemblies.SelectMany(a => GetViewExtensions(a));

            if (force || !_viewExtensions.SetEquals(viewExtensions))
            {
                _previousViewExtensionsAssemblies = _viewExtensionsAssemblies;
                _viewExtensionsAssemblies = assemblies.ToImmutableList();
                _viewExtensions.Clear();
                _viewExtensions.UnionWith(viewExtensions);

                _renderHandle.Render(_renderFragment);
            }
        }

        private void Render(RenderTreeBuilder builder)
        {
            Debug.Assert(_viewExtensions != null);
            foreach (var viewExtension in _viewExtensions!)
            {
                builder.OpenComponent(sequence: 0, viewExtension);
                ApplyParameters(builder);
                builder.CloseComponent();
            }
        }

        private void ApplyParameters(RenderTreeBuilder builder)
        {
            if (Context != null)
            {
                builder.AddAttribute(0, nameof(Context), Context);
            }

            if (ViewExtensionAttributes != null)
            {
                builder.AddMultipleAttributes(sequence: 0, ViewExtensionAttributes);
            }
        }
    }
}
