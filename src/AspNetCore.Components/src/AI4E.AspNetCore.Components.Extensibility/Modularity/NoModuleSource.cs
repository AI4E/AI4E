using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class NoModuleSource : IBlazorModuleSource
    {
        public static NoModuleSource Instance { get; } = new NoModuleSource();

        private NoModuleSource() { }

        public IAsyncEnumerable<IBlazorModuleDescriptor> GetModulesAsync(CancellationToken cancellation)
        {
            return AsyncEnumerable.Empty<IBlazorModuleDescriptor>();
        }

        public event EventHandler? ModulesChanged
        {
            add { }
            remove { }
        }
    }
}
