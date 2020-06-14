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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using AI4E.Utils.Delegation;

namespace AI4E.Utils.Projection
{
    // Recursively 'projects' any assemblies, modules, types and members returned by a given assembly
    internal class ProjectingAssembly : DelegatingAssembly, IProjectable
    {
        public ProjectingAssembly(Assembly assembly, Projector projector)
            : base(assembly)
        {
            Debug.Assert(null != projector);

            Projector = projector!;
        }

        public Projector Projector { get; }

        public override Module ManifestModule => Projector.ProjectModule(base.ManifestModule);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            attributeType = Projector.Unproject(attributeType);

            return base.GetCustomAttributes(attributeType, inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return Projector.Project(base.GetCustomAttributesData(), Projector.ProjectCustomAttributeData!);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            attributeType = Projector.Unproject(attributeType);

            return base.IsDefined(attributeType, inherit);
        }

        public override MethodInfo? EntryPoint => Projector.ProjectMethod(base.EntryPoint);

        public override Type[] GetExportedTypes()
        {
            return Projector.Project(base.GetExportedTypes(), Projector.ProjectType!);
        }

        public override Module[] GetLoadedModules(bool getResourceModules)
        {
            return Projector.Project(base.GetLoadedModules(getResourceModules), Projector.ProjectModule!);
        }

        public override ManifestResourceInfo? GetManifestResourceInfo(string resourceName)
        {
            return Projector.ProjectManifestResource(base.GetManifestResourceInfo(resourceName));
        }

        public override Module? GetModule(string name)
        {
            return Projector.ProjectModule(base.GetModule(name));
        }

        public override Module[] GetModules(bool getResourceModules)
        {
            return Projector.Project(base.GetModules(getResourceModules), Projector.ProjectModule!);
        }

        public override Assembly GetSatelliteAssembly(CultureInfo culture)
        {
            return Projector.ProjectAssembly(base.GetSatelliteAssembly(culture));
        }

        public override Assembly GetSatelliteAssembly(CultureInfo culture, Version? version)
        {
            return Projector.ProjectAssembly(base.GetSatelliteAssembly(culture, version));
        }

        public override Type? GetType(string name, bool throwOnError, bool ignoreCase)
        {
            return Projector.ProjectType(base.GetType(name, throwOnError, ignoreCase));
        }

        public override Type[] GetTypes()
        {
            return Projector.Project(base.GetTypes(), Projector.ProjectType!);
        }

        public override Module LoadModule(string moduleName, byte[]? rawModule, byte[]? rawSymbolStore)
        {
            return Projector.ProjectModule(base.LoadModule(moduleName, rawModule, rawSymbolStore));
        }

        public override bool Equals(object? o)
        {
            return o is ProjectingAssembly other &&
                   Projector == other.Projector &&
                   UnderlyingAssembly == other.UnderlyingAssembly;
        }

        public override int GetHashCode()
        {
            return Projector.GetHashCode() ^ UnderlyingAssembly.GetHashCode();
        }
    }
}
