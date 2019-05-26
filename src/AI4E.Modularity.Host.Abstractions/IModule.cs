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

using System.Collections.Generic;
using AI4E.Modularity.Metadata;

namespace AI4E.Modularity.Host
{
    public interface IModule
    {
        ModuleIdentifier Id { get; }

        IModuleRelease InstalledRelease { get; }
        ModuleVersion? InstalledVersion { get; }
        bool IsInstalled { get; }
        bool IsLatestReleaseInstalled { get; }
        IModuleRelease LatestRelease { get; }
        ModuleVersion LatestVersion { get; }
        IEnumerable<IModuleRelease> Releases { get; }

        IModuleRelease AddRelease(IModuleMetadata metadata, IModuleSource moduleSource);
        IModuleRelease GetLatestRelease(bool includePreReleases);
        IEnumerable<IModuleRelease> GetMatchingReleases(ModuleVersionRange versionRange);
        IModuleRelease GetRelease(ModuleVersion version);
        void Install();
        void Uninstall();
    }
}
