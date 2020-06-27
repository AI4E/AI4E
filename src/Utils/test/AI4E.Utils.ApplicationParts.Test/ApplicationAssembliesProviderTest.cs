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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using Xunit;

namespace AI4E.Utils.ApplicationParts.Test
{
    public class ApplicationAssembliesProviderTest
    {
        private static readonly Assembly ThisAssembly = typeof(ApplicationAssembliesProviderTest).Assembly;

        [Fact]
        public void ResolveAssemblies_ReturnsCurrentAssembly_IfNoDepsFileIsPresent()
        {
            // Arrange
            var provider = new TestApplicationAssembliesProvider();

            // Act
            var result = provider.ResolveAssemblies(ThisAssembly);

            // Assert
            Assert.Equal(new[] { ThisAssembly }, result);
        }

        [Fact]
        public void ResolveAssemblies_ReturnsCurrentAssembly_IfDepsFileDoesNotHaveAnyCompileLibraries()
        {
            // Arrange
            var runtimeLibraries = new[]
            {
                GetRuntimeLibrary("MyApp", "Microsoft.AspNetCore.App"),
                GetRuntimeLibrary("Microsoft.AspNetCore.App", "Microsoft.NETCore.App"),
                GetRuntimeLibrary("Microsoft.NETCore.App"),
                GetRuntimeLibrary("ClassLibrary"),
            };
            var dependencyContext = GetDependencyContext(compileLibraries: Array.Empty<CompilationLibrary>(), runtimeLibraries);
            var provider = new TestApplicationAssembliesProvider
            {
                DependencyContext = dependencyContext,
            };

            // Act
            var result = provider.ResolveAssemblies(ThisAssembly);

            // Assert
            Assert.Equal(new[] { ThisAssembly }, result);
        }

        // [Fact] // See: https://github.com/AI4E/AI4E/issues/277
        public void ResolveAssemblies_ReturnsRelatedAssembliesOrderedByName()
        {
            // Arrange
            var assembly1 = typeof(ApplicationAssembliesProvider).Assembly;
            var assembly2 = typeof(IActionResult).Assembly;
            var assembly3 = typeof(FactAttribute).Assembly;

            var relatedAssemblies = new[] { assembly1, assembly2, assembly3 };
            var provider = new TestApplicationAssembliesProvider
            {
                GetRelatedAssembliesDelegate = (assembly) => relatedAssemblies,
            };

            // Act
            var result = provider.ResolveAssemblies(ThisAssembly);

            // Assert
            Assert.Equal(new[] { ThisAssembly, assembly2, assembly1, assembly3 }, result);
        }

        [Fact]
        public void ResolveAssemblies_ReturnsLibrariesFromTheDepsFileThatReferenceMvc()
        {
            // Arrange
            var mvcAssembly = typeof(IActionResult).Assembly;
            var classLibrary = typeof(FactAttribute).Assembly;

            var libraries = new Dictionary<string, string[]>
            {
                [ThisAssembly.GetName().Name] = new[] { mvcAssembly.GetName().Name, classLibrary.GetName().Name },
                [mvcAssembly.GetName().Name] = Array.Empty<string>(),
                [classLibrary.GetName().Name] = new[] { mvcAssembly.GetName().Name },
            };

            var dependencyContext = GetDependencyContext(libraries);

            var provider = new TestApplicationAssembliesProvider
            {
                DependencyContext = dependencyContext,
            };

            // Act
            var result = provider.ResolveAssemblies(ThisAssembly);

            // Assert
            Assert.Equal(new[] { ThisAssembly, classLibrary, }, result);
        }

        [Fact]
        public void ResolveAssemblies_ReturnsRelatedAssembliesForLibrariesFromDepsFile()
        {
            // Arrange
            var mvcAssembly = typeof(IActionResult).Assembly;
            var classLibrary = typeof(object).Assembly;
            var relatedPart = typeof(FactAttribute).Assembly;

            var libraries = new Dictionary<string, string[]>
            {
                [ThisAssembly.GetName().Name] = new[] { relatedPart.GetName().Name, classLibrary.GetName().Name },
                [classLibrary.GetName().Name] = new[] { mvcAssembly.GetName().Name },
                [relatedPart.GetName().Name] = new[] { mvcAssembly.GetName().Name },
                [mvcAssembly.GetName().Name] = Array.Empty<string>(),
            };

            var dependencyContext = GetDependencyContext(libraries);

            var provider = new TestApplicationAssembliesProvider
            {
                DependencyContext = dependencyContext,
                GetRelatedAssembliesDelegate = (assembly) =>
                {
                    if (assembly == classLibrary)
                    {
                        return new[] { relatedPart };
                    }

                    return Array.Empty<Assembly>();
                },
            };

            // Act
            var result = provider.ResolveAssemblies(ThisAssembly);

            // Assert
            Assert.Equal(new[] { ThisAssembly, classLibrary, relatedPart, }, result);
        }

        [Fact]
        public void ResolveAssemblies_ThrowsIfRelatedAssemblyDefinesAdditionalRelatedAssemblies()
        {
            // Arrange
            var expected = $"Assembly 'TestRelatedAssembly' declared as a related assembly by assembly '{ThisAssembly}' cannot define additional related assemblies.";
            var assembly1 = typeof(ApplicationAssembliesProvider).Assembly;
            var assembly2 = new TestAssembly();

            var relatedAssemblies = new[] { assembly1, assembly2 };
            var provider = new TestApplicationAssembliesProvider
            {
                GetRelatedAssembliesDelegate = (assembly) => relatedAssemblies,
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.ResolveAssemblies(ThisAssembly).ToArray());
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void ResolveAssemblies_ThrowsIfMultipleAssembliesDeclareTheSameRelatedPart()
        {
            // Arrange
            var mvcAssembly = typeof(IActionResult).Assembly;
            var libraryAssembly1 = typeof(HttpContext).Assembly;
            var libraryAssembly2 = typeof(JsonConverter).Assembly;
            var relatedPart = typeof(FactAttribute).Assembly;
            var expected = string.Join(
                Environment.NewLine,
                $"Each related assembly must be declared by exactly one assembly. The assembly '{relatedPart.FullName}' was declared as related assembly by the following:",
                libraryAssembly1.FullName,
                libraryAssembly2.FullName);

            var libraries = new Dictionary<string, string[]>
            {
                [ThisAssembly.GetName().Name] = new[] { relatedPart.GetName().Name, libraryAssembly1.GetName().Name },
                [libraryAssembly1.GetName().Name] = new[] { mvcAssembly.GetName().Name },
                [libraryAssembly2.GetName().Name] = new[] { mvcAssembly.GetName().Name },
                [mvcAssembly.GetName().Name] = Array.Empty<string>(),
            };

            var dependencyContext = GetDependencyContext(libraries);

            var provider = new TestApplicationAssembliesProvider
            {
                DependencyContext = dependencyContext,
                GetRelatedAssembliesDelegate = (assembly) =>
                {
                    if (assembly == libraryAssembly1 || assembly == libraryAssembly2)
                    {
                        return new[] { relatedPart };
                    }

                    return Array.Empty<Assembly>();
                },
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.ResolveAssemblies(ThisAssembly).ToArray());
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void CandidateResolver_ThrowsIfDependencyContextContainsDuplicateRuntimeLibraryNames()
        {
            // Arrange
            var upperCaseLibrary = "Microsoft.AspNetCore.Mvc";
            var mixedCaseLibrary = "microsoft.aspNetCore.mvc";

            var libraries = new Dictionary<string, string[]>
            {
                [upperCaseLibrary] = Array.Empty<string>(),
                [mixedCaseLibrary] = Array.Empty<string>(),
            };
            var dependencyContext = GetDependencyContext(libraries);

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext).ToArray());

            // Assert
            Assert.Equal($"A duplicate entry for library reference {mixedCaseLibrary} was found. Please check that all package references in all projects use the same casing for the same package references.", exception.Message);
        }

        [Fact]
        public void GetCandidateLibraries_IgnoresMvcAssemblies()
        {
            // Arrange
            var expected = GetRuntimeLibrary("SomeRandomAssembly", "Microsoft.AspNetCore.Mvc.Abstractions");
            var runtimeLibraries = new[]
            {
                GetRuntimeLibrary("Microsoft.AspNetCore.Mvc.Core"),
                GetRuntimeLibrary("Microsoft.AspNetCore.Mvc"),
                GetRuntimeLibrary("Microsoft.AspNetCore.Mvc.Abstractions"),
                expected,
            };

            var compileLibraries = new[]
            {
                GetCompileLibrary("Microsoft.AspNetCore.Mvc.Core"),
                GetCompileLibrary("Microsoft.AspNetCore.Mvc"),
                GetCompileLibrary("Microsoft.AspNetCore.Mvc.Abstractions"),
                GetCompileLibrary("SomeRandomAssembly", "Microsoft.AspNetCore.Mvc.Abstractions"),
            };
            var dependencyContext = GetDependencyContext(compileLibraries, runtimeLibraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext);

            // Assert
            Assert.Equal(new[] { expected }, candidates);
        }

        [Fact]
        public void GetCandidateLibraries_ReturnsRuntimeLibraries_IfCompileLibraryDependencyToMvcIsPresent()
        {
            // Arrange
            // When an app is running against Microsoft.AspNetCore.App shared runtime or if the DependencyContext is queried
            // from an app that's running on Microsoft.NETCore.App (e.g. in a unit testing scenario), the
            // runtime library does not state that the app references Mvc whereas the compile library does. This test validates
            // that we correctly recognize this scenario.
            var expected = GetRuntimeLibrary("MyApp", "Microsoft.AspNetCore.App");
            var runtimeLibraries = new[]
            {
                expected,
                GetRuntimeLibrary("Microsoft.AspNetCore.App", "Microsoft.NETCore.App"),
                GetRuntimeLibrary("Microsoft.NETCore.App"),
            };

            var compileLibraries = new[]
            {
                GetCompileLibrary("MyApp", "Microsoft.AspNetCore.App"),
                GetCompileLibrary("Microsoft.AspNetCore.App", "Microsoft.AspNetCore.Mvc", "Microsoft.NETCore.App"),
                GetCompileLibrary("Microsoft.AspNetCore.Mvc"),
                GetCompileLibrary("Microsoft.NETCore.App"),
            };
            var dependencyContext = GetDependencyContext(compileLibraries, runtimeLibraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext);

            // Assert
            Assert.Equal(new[] { expected }, candidates);
        }

        [Fact]
        public void GetCandidateLibraries_DoesNotThrow_IfLibraryDoesNotAppearAsCompileOrRuntimeLibrary()
        {
            // Arrange
            var expected = GetRuntimeLibrary("MyApplication", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Mvc");
            var compileLibraries = new[]
            {
                GetCompileLibrary("MyApplication", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Mvc"),
                GetCompileLibrary("Microsoft.AspNetCore.Mvc"),
                GetCompileLibrary("Microsoft.AspNetCore.Server.Kestrel", "Libuv"),
            };

            var runtimeLibraries = new[]
            {
                expected,
                GetRuntimeLibrary("Microsoft.AspNetCore.Server.Kestrel", "Libuv"),
                GetRuntimeLibrary("Microsoft.AspNetCore.Mvc"),
            };

            var dependencyContext = GetDependencyContext(compileLibraries, runtimeLibraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext).ToList();

            // Assert
            Assert.Equal(new[] { expected }, candidates);
        }

        [Fact]
        public void GetCandidateLibraries_DoesNotThrow_IfCompileLibraryIsPresentButNotRuntimeLibrary()
        {
            // Arrange
            var expected = GetRuntimeLibrary("MyApplication", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Mvc");
            var compileLibraries = new[]
            {
                GetCompileLibrary("MyApplication", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Mvc"),
                GetCompileLibrary("Microsoft.AspNetCore.Mvc"),
                GetCompileLibrary("Microsoft.AspNetCore.Server.Kestrel", "Libuv"),
                GetCompileLibrary("Libuv"),
            };

            var runtimeLibraries = new[]
            {
                expected,
                GetRuntimeLibrary("Microsoft.AspNetCore.Server.Kestrel", "Libuv"),
                GetRuntimeLibrary("Microsoft.AspNetCore.Mvc"),
            };

            var dependencyContext = GetDependencyContext(compileLibraries, runtimeLibraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext).ToList();

            // Assert
            Assert.Equal(new[] { expected }, candidates);
        }

        [Fact]
        public void GetCandidateLibraries_ReturnsLibrariesReferencingAnyMvcAssembly()
        {
            // Arrange
            var libraries = new Dictionary<string, string[]>
            {
                ["Foo"] = new[] { "Microsoft.AspNetCore.Mvc.Core" },
                ["Bar"] = new[] { "Microsoft.AspNetCore.Mvc" },
                ["Qux"] = new[] { "Not.Mvc.Assembly", "Unofficial.Microsoft.AspNetCore.Mvc" },
                ["Baz"] = new[] { "Microsoft.AspNetCore.Mvc.Abstractions" },
                ["Microsoft.AspNetCore.Mvc.Core"] = Array.Empty<string>(),
                ["Microsoft.AspNetCore.Mvc"] = Array.Empty<string>(),
                ["Not.Mvc.Assembly"] = Array.Empty<string>(),
                ["Unofficial.Microsoft.AspNetCore.Mvc"] = Array.Empty<string>(),
                ["Microsoft.AspNetCore.Mvc.Abstractions"] = Array.Empty<string>(),
            };
            var dependencyContext = GetDependencyContext(libraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext);

            // Assert
            Assert.Equal(new[] { "Bar", "Baz", "Foo", }, candidates.Select(a => a.Name));
        }

        [Fact]
        public void GetCandidateLibraries_LibraryNameComparisonsAreCaseInsensitive()
        {
            // Arrange
            var libraries = new Dictionary<string, string[]>
            {
                ["Foo"] = new[] { "MICROSOFT.ASPNETCORE.MVC.CORE" },
                ["Bar"] = new[] { "microsoft.aspnetcore.mvc" },
                ["Qux"] = new[] { "Not.Mvc.Assembly", "Unofficial.Microsoft.AspNetCore.Mvc" },
                ["Baz"] = new[] { "mIcRoSoFt.AsPnEtCoRe.MvC.aBsTrAcTiOnS" },
                ["Microsoft.AspNetCore.Mvc.Core"] = Array.Empty<string>(),
                ["LibraryA"] = new[] { "LIBRARYB" },
                ["LibraryB"] = new[] { "microsoft.aspnetcore.mvc" },
                ["Microsoft.AspNetCore.Mvc"] = Array.Empty<string>(),
                ["Not.Mvc.Assembly"] = Array.Empty<string>(),
                ["Unofficial.Microsoft.AspNetCore.Mvc"] = Array.Empty<string>(),
                ["Microsoft.AspNetCore.Mvc.Abstractions"] = Array.Empty<string>(),
            };
            var dependencyContext = GetDependencyContext(libraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext);

            // Assert
            Assert.Equal(new[] { "Bar", "Baz", "Foo", "LibraryA", "LibraryB" }, candidates.Select(a => a.Name));
        }

        [Fact]
        public void GetCandidateLibraries_ReturnsLibrariesWithTransitiveReferencesToAnyMvcAssembly()
        {
            // Arrange
            var expectedLibraries = new[] { "Bar", "Baz", "Foo", "LibraryA", "LibraryB", "LibraryC", "LibraryE", "LibraryG", "LibraryH" };

            var libraries = new Dictionary<string, string[]>
            {
                ["Foo"] = new[] { "Bar" },
                ["Bar"] = new[] { "Microsoft.AspNetCore.Mvc" },
                ["Qux"] = new[] { "Not.Mvc.Assembly", "Unofficial.Microsoft.AspNetCore.Mvc" },
                ["Baz"] = new[] { "Microsoft.AspNetCore.Mvc.Abstractions" },
                ["Microsoft.AspNetCore.Mvc"] = Array.Empty<string>(),
                ["Not.Mvc.Assembly"] = Array.Empty<string>(),
                ["Microsoft.AspNetCore.Mvc.Abstractions"] = Array.Empty<string>(),
                ["Unofficial.Microsoft.AspNetCore.Mvc"] = Array.Empty<string>(),
                ["LibraryA"] = new[] { "LibraryB" },
                ["LibraryB"] = new[] { "LibraryC" },
                ["LibraryC"] = new[] { "LibraryD", "Microsoft.AspNetCore.Mvc.Abstractions" },
                ["LibraryD"] = Array.Empty<string>(),
                ["LibraryE"] = new[] { "LibraryF", "LibraryG" },
                ["LibraryF"] = Array.Empty<string>(),
                ["LibraryG"] = new[] { "LibraryH" },
                ["LibraryH"] = new[] { "LibraryI", "Microsoft.AspNetCore.Mvc" },
                ["LibraryI"] = Array.Empty<string>(),
            };
            var dependencyContext = GetDependencyContext(libraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext);

            // Assert
            Assert.Equal(expectedLibraries, candidates.Select(a => a.Name));
        }

        [Fact]
        public void GetCandidateLibraries_SkipsMvcAssemblies()
        {
            // Arrange
            var libraries = new Dictionary<string, string[]>
            {
                ["MvcSandbox"] = new[] { "Microsoft.AspNetCore.Mvc.Core", "Microsoft.AspNetCore.Mvc" },
                ["Microsoft.AspNetCore.Mvc.Core"] = new[] { "Microsoft.AspNetCore.HttpAbstractions" },
                ["Microsoft.AspNetCore.HttpAbstractions"] = Array.Empty<string>(),
                ["Microsoft.AspNetCore.Mvc"] = new[] { "Microsoft.AspNetCore.Mvc.Abstractions", "Microsoft.AspNetCore.Mvc.Core" },
                ["Microsoft.AspNetCore.Mvc.Abstractions"] = Array.Empty<string>(),
                ["Microsoft.AspNetCore.Mvc.TagHelpers"] = new[] { "Microsoft.AspNetCore.Mvc.Razor" },
                ["Microsoft.AspNetCore.Mvc.Razor"] = Array.Empty<string>(),
                ["ControllersAssembly"] = new[] { "Microsoft.AspNetCore.Mvc" },
            };

            var dependencyContext = GetDependencyContext(libraries);

            // Act
            var candidates = ApplicationAssembliesProvider.GetCandidateLibraries(dependencyContext);

            // Assert
            Assert.Equal(new[] { "ControllersAssembly", "MvcSandbox" }, candidates.Select(a => a.Name));
        }

        private class TestApplicationAssembliesProvider : ApplicationAssembliesProvider
        {
            public DependencyContext DependencyContext { get; set; }

            public Func<Assembly, IReadOnlyList<Assembly>> GetRelatedAssembliesDelegate { get; set; } = (assembly) => Array.Empty<Assembly>();

            protected override DependencyContext LoadDependencyContext(Assembly assembly) => DependencyContext;

            protected override IReadOnlyList<Assembly> GetRelatedAssemblies(Assembly assembly) => GetRelatedAssembliesDelegate(assembly);

            protected override IEnumerable<Assembly> GetLibraryAssemblies(DependencyContext dependencyContext, RuntimeLibrary runtimeLibrary)
            {
                var assemblyName = new AssemblyName(runtimeLibrary.Name);
                yield return Assembly.Load(assemblyName);
            }
        }

        private static DependencyContext GetDependencyContext(IDictionary<string, string[]> libraries)
        {
            var compileLibraries = new List<CompilationLibrary>();
            var runtimeLibraries = new List<RuntimeLibrary>();

            foreach (var kvp in libraries.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
            {
                var compileLibrary = GetCompileLibrary(kvp.Key, kvp.Value);
                compileLibraries.Add(compileLibrary);

                var runtimeLibrary = GetRuntimeLibrary(kvp.Key, kvp.Value);
                runtimeLibraries.Add(runtimeLibrary);
            }

            return GetDependencyContext(compileLibraries, runtimeLibraries);
        }

        private static DependencyContext GetDependencyContext(
            IReadOnlyList<CompilationLibrary> compileLibraries,
            IReadOnlyList<RuntimeLibrary> runtimeLibraries)
        {
            var dependencyContext = new DependencyContext(
                new TargetInfo("framework", "runtime", "signature", isPortable: true),
                CompilationOptions.Default,
                compileLibraries,
                runtimeLibraries,
                Enumerable.Empty<RuntimeFallbacks>());
            return dependencyContext;
        }

        private static RuntimeLibrary GetRuntimeLibrary(string name, params string[] dependencyNames)
        {
            var dependencies = dependencyNames?.Select(d => new Dependency(d, "42.0.0")) ?? new Dependency[0];

            return new RuntimeLibrary(
                "package",
                name,
                "23.0.0",
                "hash",
                new RuntimeAssetGroup[0],
                new RuntimeAssetGroup[0],
                new ResourceAssembly[0],
                dependencies: dependencies.ToArray(),
                serviceable: true);
        }

        private static CompilationLibrary GetCompileLibrary(string name, params string[] dependencyNames)
        {
            var dependencies = dependencyNames?.Select(d => new Dependency(d, "42.0.0")) ?? new Dependency[0];

            return new CompilationLibrary(
                "package",
                name,
                "23.0.0",
                "hash",
                Enumerable.Empty<string>(),
                dependencies: dependencies.ToArray(),
                serviceable: true);
        }

        private class TestAssembly : Assembly
        {
            public override string FullName => "TestRelatedAssembly";

            public override bool IsDefined(Type attributeType, bool inherit)
            {
                return attributeType == typeof(RelatedAssemblyAttribute);
            }
        }
    }
}
