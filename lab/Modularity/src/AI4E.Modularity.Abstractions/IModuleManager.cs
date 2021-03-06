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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Modularity.Metadata;

namespace AI4E.Modularity
{
    public interface IModuleManager
    {
        Task AddModuleAsync(ModuleIdentifier module, ModuleProperties properties, bool overrideExisting, CancellationToken cancellation = default);
        Task RemoveModuleAsync(ModuleIdentifier module, CancellationToken cancellation);

        ValueTask<IEnumerable<RouteEndPointAddress>> GetEndPointsAsync(ReadOnlyMemory<char> prefix, CancellationToken cancellation = default);
        ValueTask<ModuleProperties> GetPropertiesAsync(ModuleIdentifier module, CancellationToken cancellation = default);
    }

    public static class ModuleManagerExtension
    {
        public static Task AddModuleAsync(
            this IModuleManager moduleManager,
            ModuleIdentifier module,
            RouteEndPointAddress endPoint,
            IEnumerable<ReadOnlyMemory<char>> prefixes,
            CancellationToken cancellation = default)
        {
            var properties = new ModuleProperties(prefixes.Select(p => p.ConvertToString()).ToImmutableList(), ImmutableList.Create(endPoint));

            return moduleManager.AddModuleAsync(module, properties, overrideExisting: true, cancellation);
        }
    }
}
