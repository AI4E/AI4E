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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Modularity.Metadata;
using Newtonsoft.Json;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleInstallationConfiguration : AggregateRoot<SingletonId> // TODO: Rename
    {
        public ModuleInstallationConfiguration() : base(id: default)
        {
            //_installedModules = new Dictionary<ModuleIdentifier, ModuleVersion>();
        }

        [JsonProperty]
        public ResolvedInstallationSet ResolvedModules { get; private set; }

        [JsonProperty]
        public UnresolvedInstallationSet InstalledModules { get; private set; }

        public Task ModuleInstalledAsync(ModuleIdentifier module,
                                         ModuleVersion version,
                                         IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                         CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (InstalledModules.ContainsModule(module))
                throw new InvalidOperationException("The specified module is already installed.");

            InstalledModules = InstalledModules.WithUnresolved(module, ModuleVersionRange.SingleVersion(version));

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        public Task ModuleUpdatedAsync(ModuleIdentifier module,
                                       ModuleVersion version,
                                       IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                       CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (!InstalledModules.ContainsModule(module))
                throw new InvalidOperationException("The specified module is not installed.");

            InstalledModules = InstalledModules.SetVersionRange(module, ModuleVersionRange.SingleVersion(version));

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        public Task ModuleUninstalledAsync(ModuleIdentifier module,
                                           IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                           CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            InstalledModules = InstalledModules.WithoutUnresolved(module);

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        public Task ReleaseAddedAsync(ModuleIdentifier module,
                                      ModuleVersion version,
                                      IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                      CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            // We must update if the resolved installation set contains our module
            // -- OR --
            // The unresolved installation set is non-epty but we were unable to get to a resolved installation set (f.e. due to missing module-releases)
            if (ResolvedModules.ContainsModule(module) || InstalledModules.Unresolved.Any() && !ResolvedModules.Resolved.Any())
            {
                return ResolveDependenciesAsync(dependencyResolver, cancellation);
            }

            return Task.CompletedTask;
        }

        public Task ReleaseRemovedAsync(ModuleIdentifier module,
                                        ModuleVersion version,
                                        IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                        CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            // TODO: Is this ok?
            if (!ResolvedModules.ContainsModule(module))
            {
                return Task.CompletedTask;
            }

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        private async Task ResolveDependenciesAsync(IDependencyResolver dependencyResolver,
                                                    CancellationToken cancellation)
        {
            var unresolvedDependencies = InstalledModules.Unresolved;
            var unresolvedInstallationSet = new UnresolvedInstallationSet(resolved: Enumerable.Empty<ModuleReleaseIdentifier>(),
                                                                          unresolved: unresolvedDependencies);

            var resolvedInstallationSets = (await unresolvedInstallationSet.ResolveAsync(dependencyResolver, cancellation)).ToList();

            if (resolvedInstallationSets.Count() == 0)
            {
                Notify(new InstallationSetConflict());

                // TODO: Replace with logging. We cannot access the logger in the domain currently, as we do not have DI in the domain.
                Console.WriteLine("---> InstallationSetConflict");
            }
            else
            {
                resolvedInstallationSets.Sort();
                ResolvedModules = resolvedInstallationSets.First();
                Notify(new InstallationSetChanged(ResolvedModules));

                // TODO: Replace with logging. We cannot access the logger in the domain currently, as we do not have DI in the domain.
                Console.WriteLine("---> InstallationSetChanged: ");

                foreach (var release in ResolvedModules.Resolved)
                {
                    Console.WriteLine(release.Module + " " + release.Version);
                }
            }
        }
    }
}
