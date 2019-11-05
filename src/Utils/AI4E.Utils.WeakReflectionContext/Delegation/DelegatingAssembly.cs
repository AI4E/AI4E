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
using System.IO;
using System.Reflection;
using System.Security;

namespace AI4E.Utils.Delegation
{
    internal class DelegatingAssembly : Assembly
    {
        private readonly WeakReference<Assembly> _underlyingAssembly;

        // We cannot override ModuleResolve and Permissionset because they are critical.
        // Users will get NotImplementedException when calling these two APIs.

        public DelegatingAssembly(Assembly assembly)
        {
            Debug.Assert(null != assembly);

            _underlyingAssembly = new WeakReference<Assembly>(assembly);
        }

        public Assembly UnderlyingAssembly
        {
            get
            {
                if (_underlyingAssembly.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override string Location => UnderlyingAssembly.Location;

        public override Module ManifestModule => UnderlyingAssembly.ManifestModule;

        public override bool ReflectionOnly => UnderlyingAssembly.ReflectionOnly;

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingAssembly.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingAssembly.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingAssembly.GetCustomAttributesData();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingAssembly.IsDefined(attributeType, inherit);
        }

        public override string ToString()
        {
            return UnderlyingAssembly.ToString();
        }

        public override SecurityRuleSet SecurityRuleSet => UnderlyingAssembly.SecurityRuleSet;

        public override string? CodeBase => UnderlyingAssembly.CodeBase;

        public override object? CreateInstance(string typeName, bool ignoreCase, BindingFlags bindingAttr, Binder? binder, object[]? args, CultureInfo? culture, object[]? activationAttributes)
        {
            return UnderlyingAssembly.CreateInstance(typeName, ignoreCase, bindingAttr, binder, args, culture, activationAttributes);
        }

        public override MethodInfo? EntryPoint => UnderlyingAssembly.EntryPoint;

        public override string EscapedCodeBase => UnderlyingAssembly.EscapedCodeBase;

        public override string? FullName => UnderlyingAssembly.FullName;

        public override Type[] GetExportedTypes()
        {
            return UnderlyingAssembly.GetExportedTypes();
        }

        public override FileStream? GetFile(string name)
        {
            return UnderlyingAssembly.GetFile(name);
        }

        public override FileStream[] GetFiles()
        {
            return UnderlyingAssembly.GetFiles();
        }

        public override FileStream[] GetFiles(bool getResourceModules)
        {
            return UnderlyingAssembly.GetFiles(getResourceModules);
        }

        public override Module[] GetLoadedModules(bool getResourceModules)
        {
            return UnderlyingAssembly.GetLoadedModules(getResourceModules);
        }

        public override ManifestResourceInfo? GetManifestResourceInfo(string resourceName)
        {
            return UnderlyingAssembly.GetManifestResourceInfo(resourceName);
        }

        public override string[] GetManifestResourceNames()
        {
            return UnderlyingAssembly.GetManifestResourceNames();
        }

        public override Stream? GetManifestResourceStream(string name)
        {
            return UnderlyingAssembly.GetManifestResourceStream(name);
        }

        public override Stream? GetManifestResourceStream(Type type, string name)
        {
            return UnderlyingAssembly.GetManifestResourceStream(type, name);
        }

        public override Module? GetModule(string name)
        {
            return UnderlyingAssembly.GetModule(name);
        }

        public override Module[] GetModules(bool getResourceModules)
        {
            return UnderlyingAssembly.GetModules(getResourceModules);
        }

        public override AssemblyName GetName()
        {
            return UnderlyingAssembly.GetName();
        }

        public override AssemblyName GetName(bool copiedName)
        {
            return UnderlyingAssembly.GetName(copiedName);
        }

        public override AssemblyName[] GetReferencedAssemblies()
        {
            return UnderlyingAssembly.GetReferencedAssemblies();
        }

        public override Assembly GetSatelliteAssembly(CultureInfo culture)
        {
            return UnderlyingAssembly.GetSatelliteAssembly(culture);
        }

        public override Assembly GetSatelliteAssembly(CultureInfo culture, Version? version)
        {
            return UnderlyingAssembly.GetSatelliteAssembly(culture, version);
        }

        public override Type? GetType(string name, bool throwOnError, bool ignoreCase)
        {
            return UnderlyingAssembly.GetType(name, throwOnError, ignoreCase);
        }

        public override Type[] GetTypes()
        {
            return UnderlyingAssembly.GetTypes();
        }

        public override bool GlobalAssemblyCache => UnderlyingAssembly.GlobalAssemblyCache;

        public override long HostContext => UnderlyingAssembly.HostContext;

        public override string ImageRuntimeVersion => UnderlyingAssembly.ImageRuntimeVersion;

        public override bool IsDynamic => UnderlyingAssembly.IsDynamic;

        public override Module LoadModule(string moduleName, byte[]? rawModule, byte[]? rawSymbolStore)
        {
            return UnderlyingAssembly.LoadModule(moduleName, rawModule, rawSymbolStore);
        }
    }
}
