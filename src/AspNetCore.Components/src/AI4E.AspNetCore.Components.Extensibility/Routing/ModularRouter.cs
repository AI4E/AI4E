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
using System.Diagnostics;
using System.Linq;
using AI4E.AspNetCore.Components.Extensibility;
using Microsoft.AspNetCore.Components;

namespace AI4E.AspNetCore.Components.Routing
{
    /// <summary>
    /// A router that loads the component assemblies from a <see cref="IAssemblySource"/>.
    /// </summary>
    public class ModularRouter : ExtensibleRouter
    {
        private IAssemblySource? _assemblySource;

        private IAssemblySource AssemblySource => _assemblySource ??= AssemblyRegistry.AssemblySource;

        protected internal RouteData? PreviousRouteData { get; private set; }

        [Inject] private IAssemblyRegistry AssemblyRegistry { get; set; } = null!;

        /// <inheritdoc />
        protected override IEnumerable<Type> ResolveRoutableComponents()
        {
            return AssemblySource.Assemblies.SelectMany(p => ComponentResolver.GetComponents(p));
        }

        /// <inheritdoc />
        protected override void OnInit()
        {
            if (AssemblyRegistry != null)
            {
                AssemblyRegistry.AssemblySourceChanged += AssemblySourceChanged;
            }

            base.OnInit();
        }

        private void AssemblySourceChanged(object? sender, EventArgs args)
        {
            _ = InvokeAsync(() =>
            {
                _assemblySource = null;

                UpdateRouteTable();

                // Check whether we have to refresh. This is the case if any of:
                // - The last routing was not successful
                // - The current route handler is of an assembly that is unavailable (is currently in an unload process) assembly
                // - The previous route handle is of an assembly that is unavailable assembly (the components and types are still stored in the render tree for diff building)
                if (NeedsRefresh(out var routeIsOfUnloadedAssembly))
                {
                    Refresh();
                }

                // We need to refresh again, if the route handle before the refresh above is of an assembly that is unavailable assembly.
                // With the above refresh the components and types are still stored in the render tree for diff building and we have to refresh again to remove them.
                if (routeIsOfUnloadedAssembly)
                {
                    Refresh();
                }
            });
        }

        private bool NeedsRefresh(out bool routeIsOfUnloadedAssembly)
        {
            routeIsOfUnloadedAssembly = false;

            if (RouteData is null)
            {
                return true;
            }

            if (RouteIsOfUnloadedAssembly())
            {
                routeIsOfUnloadedAssembly = true;
                return true;
            }

            return PreviousRouteIsOfUnloadedAssembly();
        }

        private bool RouteIsOfUnloadedAssembly()
        {
            Debug.Assert(RouteData != null);
            var pageType = RouteData!.PageType;
            var pageTypeAssembly = pageType.Assembly;

            return !AssemblySource.ContainsAssembly(pageTypeAssembly);
        }

        private bool PreviousRouteIsOfUnloadedAssembly()
        {
            if (PreviousRouteData is null)
            {
                return false;
            }

            var previousPageType = PreviousRouteData.PageType;
            var previousPageTypeAssembly = previousPageType.Assembly;

            return !AssemblySource.ContainsAssembly(previousPageTypeAssembly);
        }

        protected override void OnAfterRefresh(bool success)
        {
            PreviousRouteData = RouteData;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (AssemblySource != null)
            {
                AssemblyRegistry.AssemblySourceChanged -= AssemblySourceChanged;
            }

            base.Dispose(disposing);
        }
    }
}
