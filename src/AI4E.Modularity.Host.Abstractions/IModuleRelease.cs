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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;

namespace AI4E.Modularity.Host
{
    public interface IModuleRelease
    {
        string Author { get; }
        IEnumerable<ModuleDependency> Dependencies { get; }
        string Description { get; }
        ModuleReleaseIdentifier Id { get; }
        bool IsInstalled { get; }
        IModule Module { get; }
        string Name { get; }
        DateTime ReleaseDate { get; }
        ModuleVersion Version { get; }

        ValueTask<IEnumerable<IModuleSource>> GetSourcesAsync(CancellationToken cancellation);

        bool TryAddSource(IModuleSource source);
        void Install();
        bool TryRemoveSource(IModuleSource source);
        void Uninstall();
    }
}
