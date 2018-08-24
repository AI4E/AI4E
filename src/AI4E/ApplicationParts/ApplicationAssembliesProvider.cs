/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
* Asp.Net Core MVC
* Copyright (c) .NET Foundation. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use
* these files except in compliance with the License. You may obtain a copy of the
* License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed
* under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
* CONDITIONS OF ANY KIND, either express or implied. See the License for the
* specific language governing permissions and limitations under the License.
* --------------------------------------------------------------------------------------------------------------------
*/

using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AI4E.ApplicationParts
{
    internal class ApplicationAssembliesProvider
    {
        internal static HashSet<string> ReferenceAssemblies { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // The deps file for the Microsoft.AspNetCore.All shared runtime is authored in a way where it does not say
            // it depends on Microsoft.AspNetCore.Mvc even though it does. Explicitly list it so that referencing this runtime causes
            // assembly discovery to work correctly.
            "Microsoft.AspNetCore.All",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Mvc.Abstractions",
            "Microsoft.AspNetCore.Mvc.ApiExplorer",
            "Microsoft.AspNetCore.Mvc.Core",
            "Microsoft.AspNetCore.Mvc.Cors",
            "Microsoft.AspNetCore.Mvc.DataAnnotations",
            "Microsoft.AspNetCore.Mvc.Formatters.Json",
            "Microsoft.AspNetCore.Mvc.Formatters.Xml",
            "Microsoft.AspNetCore.Mvc.Localization",
            "Microsoft.AspNetCore.Mvc.Razor",
            "Microsoft.AspNetCore.Mvc.Razor.Extensions",
            "Microsoft.AspNetCore.Mvc.RazorPages",
            "Microsoft.AspNetCore.Mvc.TagHelpers",
            "Microsoft.AspNetCore.Mvc.ViewFeatures",
        };

        /// <summary>
        /// Returns an ordered list of application assemblies.
        /// <para>
        /// The order is as follows:
        /// * Entry assembly
        /// * Assemblies specified in the application's deps file ordered by name.
        /// <para>
        /// Each assembly is immediately followed by assemblies specified by annotated <see cref="RelatedAssemblyAttribute"/> ordered by name.
        /// </para>
        /// </para>
        /// </summary>
        public IEnumerable<Assembly> ResolveAssemblies(Assembly entryAssembly)
        {
            var dependencyContext = LoadDependencyContext(entryAssembly);

            IEnumerable<AssemblyItem> assemblyItems;

            if (dependencyContext == null || dependencyContext.CompileLibraries.Count == 0)
            {
                // If an application was built with PreserveCompilationContext = false, CompileLibraries will be empty and we
                // can no longer reliably infer the dependency closure. In this case, treat it the same as a missing
                // deps file.
                assemblyItems = new[] { GetAssemblyItem(entryAssembly) };
            }
            else
            {
                assemblyItems = ResolveFromDependencyContext(dependencyContext);
            }

            assemblyItems = assemblyItems
                .OrderBy(item => item.Assembly == entryAssembly ? 0 : 1)
                .ThenBy(item => item.Assembly.FullName, StringComparer.Ordinal);

            foreach (var item in assemblyItems)
            {
                yield return item.Assembly;

                foreach (var associatedAssembly in item.RelatedAssemblies.Distinct().OrderBy(assembly => assembly.FullName, StringComparer.Ordinal))
                {
                    yield return associatedAssembly;
                }
            }
        }

        protected virtual DependencyContext LoadDependencyContext(Assembly assembly)
        {
            return DependencyContext.Load(assembly);
        }

        private List<AssemblyItem> ResolveFromDependencyContext(DependencyContext dependencyContext)
        {
            var assemblyItems = new List<AssemblyItem>();
            var relatedAssemblies = new Dictionary<Assembly, AssemblyItem>();

            var candidateAssemblies = GetCandidateLibraries(dependencyContext)
                .SelectMany(library => GetLibraryAssemblies(dependencyContext, library));

            foreach (var assembly in candidateAssemblies)
            {
                var assemblyItem = GetAssemblyItem(assembly);
                assemblyItems.Add(assemblyItem);

                foreach (var relatedAssembly in assemblyItem.RelatedAssemblies)
                {
                    if (relatedAssemblies.TryGetValue(relatedAssembly, out var otherEntry))
                    {
                        var message = string.Join(
                            Environment.NewLine,
                            string.Format("Each related assembly must be declared by exactly one assembly. The assembly '{0}' was declared as related assembly by the following:", relatedAssembly.FullName),
                            otherEntry.Assembly.FullName,
                            assembly.FullName);

                        throw new InvalidOperationException(message);
                    }

                    relatedAssemblies.Add(relatedAssembly, assemblyItem);
                }
            }

            // Remove any top level assemblies that appear as an associated assembly.
            assemblyItems.RemoveAll(item => relatedAssemblies.ContainsKey(item.Assembly));

            return assemblyItems;
        }

        protected virtual IEnumerable<Assembly> GetLibraryAssemblies(DependencyContext dependencyContext, RuntimeLibrary runtimeLibrary)
        {
            foreach (var assemblyName in runtimeLibrary.GetDefaultAssemblyNames(dependencyContext))
            {
                var assembly = Assembly.Load(assemblyName);
                yield return assembly;
            }
        }

        protected virtual IReadOnlyList<Assembly> GetRelatedAssemblies(Assembly assembly)
        {
            // Do not require related assemblies to be available in the default code path.
            return RelatedAssemblyAttribute.GetRelatedAssemblies(assembly, throwOnError: false);
        }

        private AssemblyItem GetAssemblyItem(Assembly assembly)
        {
            var relatedAssemblies = GetRelatedAssemblies(assembly);

            // Ensure we don't have any cycles. A cycle could be formed if a related assembly points to the primary assembly.
            foreach (var relatedAssembly in relatedAssemblies)
            {
                if (relatedAssembly.IsDefined(typeof(RelatedAssemblyAttribute)))
                {
                    throw new InvalidOperationException(
                        string.Format("Assembly '{0}' declared as a related assembly by assembly '{1}' cannot define additional related assemblies.", relatedAssembly.FullName, assembly.FullName));
                }
            }

            return new AssemblyItem(assembly, relatedAssemblies);
        }

        // Returns a list of libraries that references the assemblies in <see cref="ReferenceAssemblies"/>.
        // By default it returns all assemblies that reference any of the primary MVC assemblies
        // while ignoring MVC assemblies.
        // Internal for unit testing
        internal static IEnumerable<RuntimeLibrary> GetCandidateLibraries(DependencyContext dependencyContext)
        {
            // When using Microsoft.AspNetCore.App \ Microsoft.AspNetCore.All shared runtimes, entries in the RuntimeLibraries
            // get "erased" and it is no longer accurate to query to determine a library's dependency closure.
            // We'll use CompileLibraries to calculate the dependency graph and runtime library to resolve assemblies to inspect.
            var candidatesResolver = new CandidateResolver(dependencyContext.CompileLibraries, ReferenceAssemblies);
            foreach (var library in dependencyContext.RuntimeLibraries)
            {
                if (candidatesResolver.IsCandidate(library))
                {
                    yield return library;
                }
            }
        }

        private class CandidateResolver
        {
            private readonly IDictionary<string, Dependency> _compileLibraries;

            public CandidateResolver(IReadOnlyList<Library> compileLibraries, ISet<string> referenceAssemblies)
            {
                var compileLibrariesWithNoDuplicates = new Dictionary<string, Dependency>(StringComparer.OrdinalIgnoreCase);
                foreach (var library in compileLibraries)
                {
                    if (compileLibrariesWithNoDuplicates.ContainsKey(library.Name))
                    {
                        throw new InvalidOperationException(string.Format("A duplicate entry for library reference {0} was found. Please check that all package references in all projects use the same casing for the same package references.", library.Name));
                    }

                    compileLibrariesWithNoDuplicates.Add(library.Name, CreateDependency(library, referenceAssemblies));
                }

                _compileLibraries = compileLibrariesWithNoDuplicates;
            }

            public bool IsCandidate(Library library)
            {
                var classification = ComputeClassification(library.Name);
                return classification == DependencyClassification.ReferencesMvc;
            }

            private Dependency CreateDependency(Library library, ISet<string> referenceAssemblies)
            {
                var classification = DependencyClassification.Unknown;
                if (referenceAssemblies.Contains(library.Name))
                {
                    classification = DependencyClassification.MvcReference;
                }

                return new Dependency(library, classification);
            }

            private DependencyClassification ComputeClassification(string dependency)
            {
                if (!_compileLibraries.ContainsKey(dependency))
                {
                    // Library does not have runtime dependency. Since we can't infer
                    // anything about it's references, we'll assume it does not have a reference to Mvc.
                    return DependencyClassification.DoesNotReferenceMvc;
                }

                var candidateEntry = _compileLibraries[dependency];
                if (candidateEntry.Classification != DependencyClassification.Unknown)
                {
                    return candidateEntry.Classification;
                }
                else
                {
                    var classification = DependencyClassification.DoesNotReferenceMvc;
                    foreach (var candidateDependency in candidateEntry.Library.Dependencies)
                    {
                        var dependencyClassification = ComputeClassification(candidateDependency.Name);
                        if (dependencyClassification == DependencyClassification.ReferencesMvc ||
                            dependencyClassification == DependencyClassification.MvcReference)
                        {
                            classification = DependencyClassification.ReferencesMvc;
                            break;
                        }
                    }

                    candidateEntry.Classification = classification;

                    return classification;
                }
            }

            private class Dependency
            {
                public Dependency(Library library, DependencyClassification classification)
                {
                    Library = library;
                    Classification = classification;
                }

                public Library Library { get; }

                public DependencyClassification Classification { get; set; }

                public override string ToString()
                {
                    return $"Library: {Library.Name}, Classification: {Classification}";
                }
            }

            private enum DependencyClassification
            {
                Unknown = 0,

                /// <summary>
                /// References (directly or transitively) one of the Mvc packages listed in
                /// <see cref="ReferenceAssemblies"/>.
                /// </summary>
                ReferencesMvc = 1,

                /// <summary>
                /// Does not reference (directly or transitively) one of the Mvc packages listed by
                /// <see cref="ReferenceAssemblies"/>.
                /// </summary>
                DoesNotReferenceMvc = 2,

                /// <summary>
                /// One of the references listed in <see cref="ReferenceAssemblies"/>.
                /// </summary>
                MvcReference = 3,
            }
        }

        private readonly struct AssemblyItem
        {
            public AssemblyItem(Assembly assembly, IReadOnlyList<Assembly> associatedAssemblies)
            {
                Assembly = assembly;
                RelatedAssemblies = associatedAssemblies;
            }

            public Assembly Assembly { get; }

            public IReadOnlyList<Assembly> RelatedAssemblies { get; }
        }
    }
}
