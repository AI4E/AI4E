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
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Serialization;

#if MODULE
using System.Runtime.Loader;
using AI4E.AspNetCore.Components.Modularity;
#endif

namespace AI4E.Messaging.Serialization
{
    internal sealed class ContractResolver : DefaultContractResolver
    {
#if !MODULE
        public static ContractResolver Instance { get; } = new ContractResolver();
#endif
        // We cannot just use a ConditionalWeakTable<Type, JsonContract> here, because the key may be a type of another
        // context but the JsonContract type itself is loaded into our own context. If this is the case and our 
        // context tries to unload, the keys of the other contexts keep their respective JsonContract object alive
        // preventing us unloading successfully.

        // We need to register to the unload event of our context, 
        // invalidate the cache and disallow further use of the cache.

#if MODULE
        private ConditionalWeakTable<Type, JsonContract>? _contractsCache;
#else
        private readonly ConditionalWeakTable<Type, JsonContract> _contractsCache;
#endif

#if MODULE
        public ContractResolver(ModuleContext moduleContext)
#else
        private ContractResolver()
#endif

        {
            _contractsCache = new ConditionalWeakTable<Type, JsonContract>();

#if MODULE
            moduleContext.ModuleLoadContext.Unloading += ModuleUnloading;
#endif
        }

#if MODULE
        private void ModuleUnloading(AssemblyLoadContext obj)
        {
            obj.Unloading -= ModuleUnloading;

            _contractsCache = null;
        }
#endif

        public override JsonContract ResolveContract(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

#if MODULE
            return _contractsCache?.GetValue(type, CreateContract) ?? CreateContract(type);
#else
            return _contractsCache.GetValue(type, CreateContract);
#endif
        }
    }
}
