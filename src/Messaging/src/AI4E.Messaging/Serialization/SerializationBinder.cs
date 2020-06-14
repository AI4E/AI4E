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
using AI4E.Utils;
using Newtonsoft.Json.Serialization;

namespace AI4E.Messaging.Serialization
{
    internal sealed class SerializationBinder : ISerializationBinder
    {
        private readonly ITypeResolver _typeResolver;

        public SerializationBinder(ITypeResolver typeResolver)
        {
            _typeResolver = typeResolver;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            typeName = serializedType.GetUnqualifiedTypeName();
            assemblyName = null;
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            // TODO: Can we safely do this? This bypasses any reflection-context that we set up previously. 
            //       If this is used to store the type somewhere and the reflection context is a WeakReflectionContext,
            //       unload is impossible.
            //       This is needed here, because Json.Net itself needs the RuntimeType itself for performing IL-Emit.
            //       In the optimal case, we had to return different things for different callers.
            //       Can we work around this?
            return _typeResolver.ResolveType(typeName.AsSpan()).UnderlyingSystemType;
        }
    }
}
