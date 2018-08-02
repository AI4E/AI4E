using System;
using System.Collections.Generic;

namespace AI4E.Modularity
{
    public interface IModuleMetadata
    {
        ModuleIdentifier Module { get; }

        ModuleVersion Version { get; }

        ModuleReleaseIdentifier Release { get; }

        DateTime ReleaseDate { get; }

        string Name { get; }

        string Description { get; }

        string Author { get; }

        string EntryAssemblyCommand { get; }

        string EntryAssemblyArguments { get; }

        IEnumerable<ModuleDependency> Dependencies { get; }
    }
}
