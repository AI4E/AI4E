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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using AI4E.AspNetCore.Components.Modularity;
using AI4E.Utils;

namespace Routing.Modularity.Sample.Services
{
    public sealed class BlazorModuleDescriptor : IBlazorModuleDescriptor
    {
#pragma warning disable IDE0051
        [MethodImpl(MethodImplOptions.PreserveSig)]
        private BlazorModuleDescriptor(
#pragma warning restore IDE0051
            ImmutableList<IBlazorModuleAssemblyDescriptor> assemblies,
            string name,
            string urlPrefix)
        {
            Assemblies = assemblies;
            Name = name;
            UrlPrefix = urlPrefix;
        }

        public ImmutableList<IBlazorModuleAssemblyDescriptor> Assemblies { get; }

        public string Name { get; }

        public string UrlPrefix { get; }

        public static Builder CreateBuilder(string name, string urlPrefix)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            if (urlPrefix is null)
                throw new ArgumentNullException(nameof(urlPrefix));

            return new Builder(name, urlPrefix);
        }

#pragma warning disable CA1034
        public sealed class Builder
#pragma warning restore CA1034
        {
            private static readonly ConstructorInfo _ctor = GetConstructor();
            private static ConstructorInfo GetConstructor()
            {
                var result = typeof(BlazorModuleDescriptor).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    Type.DefaultBinder,
                    new[] { typeof(ImmutableList<IBlazorModuleAssemblyDescriptor>), typeof(string), typeof(string) },
                    modifiers: null);

                Debug.Assert(result != null);

                return result!;
            }

            private readonly ValueCollection<BlazorModuleAssemblyDescriptor.Builder> _assemblies;
            private string _name;
            private string _urlPrefix;

            internal Builder(string name, string urlPrefix)
            {
                _name = name;
                _urlPrefix = urlPrefix;
                _assemblies = new ValueCollection<BlazorModuleAssemblyDescriptor.Builder>();
            }

            public ICollection<BlazorModuleAssemblyDescriptor.Builder> Assemblies => _assemblies;

            public string Name
            {
                get => _name;
                set
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));

                    _name = value;
                }
            }

            public string UrlPrefix
            {
                get => _urlPrefix;
                set
                {
                    if (value is null)
                        throw new ArgumentNullException(nameof(value));

                    _urlPrefix = value;
                }
            }

            public BlazorModuleDescriptor Build()
            {
                var result = (BlazorModuleDescriptor)FormatterServices.GetUninitializedObject(
                    typeof(BlazorModuleDescriptor));

                var assemblyDescriptors = _assemblies.Select(
                    p => p.Build(result)).ToImmutableList<IBlazorModuleAssemblyDescriptor>();

                // This is rather slow but we do not expect this called very frequently.
                _ctor.Invoke(result, new object[] { assemblyDescriptors, _name, _urlPrefix });
                return result;
            }
        }
    }
}
