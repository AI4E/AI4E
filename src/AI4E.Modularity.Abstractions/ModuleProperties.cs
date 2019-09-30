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
using System.Collections.Immutable;
using System.Linq;
using AI4E.Messaging;
using AI4E.Modularity.Metadata;
using AI4E.Routing;

namespace AI4E.Modularity
{
    public sealed class ModuleProperties
    {
        public ModuleProperties(ImmutableList<string> prefixes, ImmutableList<EndPointAddress> endPoints)
        {
            if (!prefixes.Any())
                throw new ArgumentException("The collection must contain at least one entry", nameof(prefixes));

            if(!endPoints.Any())
                throw new ArgumentException("The collection must contain at least one entry", nameof(endPoints));

            if (prefixes.Any(prefix => string.IsNullOrWhiteSpace(prefix)))
                throw new ArgumentException("The collection must not contain null entries or entries that are empty or contain whitespace only.", nameof(prefixes));

            Prefixes = prefixes;
            EndPoints = endPoints;
        }

        public ImmutableList<string> Prefixes { get; }
        public ImmutableList<EndPointAddress> EndPoints { get; }
    }

    public sealed class ModulePropertiesQuery : Query<ModuleProperties>
    {
        public ModulePropertiesQuery(ModuleIdentifier module)
        {
            Module = module;
        }

        public ModuleIdentifier Module { get; }
    }
}
