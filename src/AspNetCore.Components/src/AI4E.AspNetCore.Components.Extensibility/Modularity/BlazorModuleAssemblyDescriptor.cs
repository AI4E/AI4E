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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleAssemblyDescriptor : IBlazorModuleAssemblyDescriptor
    {
        private readonly Func<CancellationToken, ValueTask<BlazorModuleAssemblySource>> _loadAssemblySourceAsync;

        private BlazorModuleAssemblyDescriptor(
            IBlazorModuleDescriptor moduleDescriptor,
            string assemblyName,
            Version assemblyVersion,
            bool isComponentAssembly,
            Func<CancellationToken, ValueTask<BlazorModuleAssemblySource>> loadAssemblySourceAsync)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (assemblyName is null)
                throw new ArgumentNullException(nameof(assemblyName));

            if (assemblyVersion is null)
                throw new ArgumentNullException(nameof(assemblyVersion));

            if (loadAssemblySourceAsync is null)
                throw new ArgumentNullException(nameof(loadAssemblySourceAsync));

            ModuleDescriptor = moduleDescriptor;
            AssemblyName = assemblyName;
            AssemblyVersion = assemblyVersion;
            IsComponentAssembly = isComponentAssembly;
            _loadAssemblySourceAsync = loadAssemblySourceAsync;
        }

        public IBlazorModuleDescriptor ModuleDescriptor { get; }

        public string AssemblyName { get; } // TODO: Does this encode the simple-name or the display-name?

        public Version AssemblyVersion { get; } // TODO: This should be nullable

        public bool IsComponentAssembly { get; }

        public ValueTask<BlazorModuleAssemblySource> LoadAssemblySourceAsync(CancellationToken cancellation)
        {
            return _loadAssemblySourceAsync(cancellation);
        }

        public static Builder CreateBuilder(
            string assemblyName,
            Version assemblyVersion,
            Func<CancellationToken, ValueTask<BlazorModuleAssemblySource>> loadAssemblySourceAsync)
        {
            if (assemblyName is null)
                throw new ArgumentNullException(nameof(assemblyName));

            if (assemblyVersion is null)
                throw new ArgumentNullException(nameof(assemblyVersion));

            if (loadAssemblySourceAsync is null)
                throw new ArgumentNullException(nameof(loadAssemblySourceAsync));

            return new Builder(assemblyName, assemblyVersion, loadAssemblySourceAsync);
        }

        public static Builder CreateBuilder(Assembly assembly, bool forceLoad = false)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            var assemblyName = assembly.GetName();

            ValueTask<BlazorModuleAssemblySource> LoadAssemblySourceAsync(CancellationToken cancellation)
            {
                return BlazorModuleAssemblySource.FromLocationAsync(assembly.Location, forceLoad, cancellation);
            }

            return new Builder(assemblyName.FullName, assemblyName.Version!, LoadAssemblySourceAsync);
        }

#pragma warning disable CA1034
        public sealed class Builder
#pragma warning restore CA1034
        {
            private string _assemblyName;
            private Version _assemblyVersion;
            private Func<CancellationToken, ValueTask<BlazorModuleAssemblySource>> _loadAssemblySourceAsync;

            internal Builder(
                string assemblyName,
                Version assemblyVersion,
                Func<CancellationToken, ValueTask<BlazorModuleAssemblySource>> loadAssemblySourceAsync)
            {
                _assemblyName = assemblyName;
                _assemblyVersion = assemblyVersion;
                _loadAssemblySourceAsync = loadAssemblySourceAsync;
            }

            internal Builder(IBlazorModuleAssemblyDescriptor assemblyDescriptor)
            {
                if (assemblyDescriptor is null)
                    throw new ArgumentNullException(nameof(assemblyDescriptor));

                _assemblyName = assemblyDescriptor.AssemblyName;
                _assemblyVersion = assemblyDescriptor.AssemblyVersion;

                if (assemblyDescriptor is BlazorModuleAssemblyDescriptor knownImpl)
                {
                    _loadAssemblySourceAsync = knownImpl._loadAssemblySourceAsync;
                }
                else
                {
                    _loadAssemblySourceAsync = assemblyDescriptor.LoadAssemblySourceAsync;
                }

                IsComponentAssembly = assemblyDescriptor.IsComponentAssembly;
            }

            public string AssemblyName
            {
                get => _assemblyName;
                set
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));

                    _assemblyName = value;
                }
            }

            public Version AssemblyVersion
            {
                get => _assemblyVersion;
                set
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));

                    _assemblyVersion = value;
                }
            }

            public bool IsComponentAssembly { get; set; }

            public Func<CancellationToken, ValueTask<BlazorModuleAssemblySource>> LoadAssemblySourceAsync
            {
                get => _loadAssemblySourceAsync;
                set
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));

                    _loadAssemblySourceAsync = value;
                }
            }

            public BlazorModuleAssemblyDescriptor Build(IBlazorModuleDescriptor moduleDescriptor)
            {
                if (moduleDescriptor is null)
                    throw new ArgumentNullException(nameof(moduleDescriptor));

                return new BlazorModuleAssemblyDescriptor(
                    moduleDescriptor,
                    _assemblyName,
                    _assemblyVersion,
                    IsComponentAssembly,
                    _loadAssemblySourceAsync);
            }
        }
    }
}
