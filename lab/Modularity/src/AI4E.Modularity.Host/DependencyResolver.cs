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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;
using AI4E.Storage.Domain;

namespace AI4E.Modularity.Host
{
    public sealed class DependencyResolver : IDependencyResolver
    {
        private readonly IEntityStorageEngine _storageEngine;

        public DependencyResolver(IEntityStorageEngine storageEngine)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            _storageEngine = storageEngine;
        }

        public async ValueTask<IEnumerable<ModuleReleaseIdentifier>> GetMatchingReleasesAsync(ModuleDependency dependency, CancellationToken cancellation)
        {
            var module = await _storageEngine.GetByIdAsync(typeof(Module), dependency.Module.ToString(), cancellation) as Module;

            if (module == null)
            {
                return Enumerable.Empty<ModuleReleaseIdentifier>(); // TODO: Is this correct?
            }

            return module.GetMatchingReleases(dependency.VersionRange).Select(p => p.Id);
        }

        public async ValueTask<IEnumerable<ModuleDependency>> GetDependenciesAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            var module = await _storageEngine.GetByIdAsync(typeof(Module), moduleRelease.Module.ToString(), cancellation) as Module;

            if (module == null)
            {
                throw new Exception(); // TODO
            }

            var release = module.GetRelease(moduleRelease.Version);

            if (release == null)
            {
                throw new Exception(); // TODO
            }

            return release.Dependencies;
        }
    }
}
