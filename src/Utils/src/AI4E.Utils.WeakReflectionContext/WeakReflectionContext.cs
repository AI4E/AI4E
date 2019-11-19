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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * corefx (https://github.com/dotnet/corefx)
 * Licensed to the .NET Foundation under one or more agreements.
 * The .NET Foundation licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Reflection;
using AI4E.Utils.Projection;

namespace AI4E.Utils
{
    public partial class WeakReflectionContext : ReflectionContext
    {
        private readonly WeakReflectionContextProjector _projector;

        public WeakReflectionContext() : this(new IdentityReflectionContext()) { }

        public WeakReflectionContext(ReflectionContext source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            SourceContext = source;
            _projector = new WeakReflectionContextProjector(this);
        }

        public override Assembly MapAssembly(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));       

            return _projector.ProjectAssemblyIfNeeded(assembly);
        }

        public override TypeInfo MapType(TypeInfo type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return _projector.ProjectTypeIfNeeded(type);
        }

        internal Projector Projector => _projector;

        internal ReflectionContext SourceContext { get; }
    }
}
