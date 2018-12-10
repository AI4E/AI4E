using System;
using System.Collections.Generic;

namespace AI4E.Modularity
{
    public interface IModuleMetadata
    {
        ModuleIdentifier Module { get; }

        ModuleVersion Version { get; }

        ModuleReleaseIdentifier Release { get; }

        DateTime ReleaseDate { get; } // TODO: This should be DateTime? actually

        string Name { get; }

        string Description { get; }

        string Author { get; }

        string EntryAssemblyCommand { get; }

        string EntryAssemblyArguments { get; }

        IEnumerable<ModuleDependency> Dependencies { get; }
    }
}
