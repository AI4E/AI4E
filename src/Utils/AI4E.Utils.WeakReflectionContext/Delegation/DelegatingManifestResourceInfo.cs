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
using System.Diagnostics;
using System.Reflection;

namespace AI4E.Utils.Delegation
{
    internal class DelegatingManifestResourceInfo : ManifestResourceInfo
    {
        private readonly WeakReference<ManifestResourceInfo> _underlyingResource;

        public DelegatingManifestResourceInfo(ManifestResourceInfo resource)
            : base(resource.ReferencedAssembly, resource.FileName, resource.ResourceLocation)
        {
            Debug.Assert(null != resource);

            _underlyingResource = new WeakReference<ManifestResourceInfo>(resource!);
        }

        public ManifestResourceInfo UnderlyingResource
        {
            get
            {
                if (_underlyingResource.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }
    }
}
