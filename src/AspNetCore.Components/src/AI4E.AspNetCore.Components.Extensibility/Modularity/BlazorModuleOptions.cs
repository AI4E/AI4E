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
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleOptions
    {
        public bool LoadSymbols { get; set; }

        public List<Func<ImmutableHashSet<Assembly>, ValueTask>> ConfigureCleanup { get; } = new List<Func<ImmutableHashSet<Assembly>, ValueTask>>();
        public List<Action<ModuleContext, IServiceCollection>> ConfigureModuleServices { get; } = new List<Action<ModuleContext, IServiceCollection>>();
        public List<Func<IBlazorModuleSource, IBlazorModuleSource>> ConfigureModuleSource { get; } = new List<Func<IBlazorModuleSource, IBlazorModuleSource>>();
    }
}
