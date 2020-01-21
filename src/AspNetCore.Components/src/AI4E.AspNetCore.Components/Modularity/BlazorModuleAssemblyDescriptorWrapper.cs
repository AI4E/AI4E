using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleAssemblyDescriptorWrapper : IBlazorModuleAssemblyDescriptor
    {
        private readonly IBlazorModuleAssemblyDescriptor _wrapped;
        private readonly IBlazorModuleDescriptor? _moduleDescriptor;
        private readonly string? _assemblyName;
        private readonly Version? _assemblyVersion;
        private readonly bool? _isComponentAssembly;
        private readonly bool? _forceLoad;

        public BlazorModuleAssemblyDescriptorWrapper(
            IBlazorModuleAssemblyDescriptor wrapped,
            IBlazorModuleDescriptor? moduleDescriptor = null,
            string? assemblyName = null,
            Version? assemblyVersion = null,
            bool? isComponentAssembly = null,
            bool? forceLoad = null)
        {
            if (wrapped is null)
                throw new ArgumentNullException(nameof(wrapped));

            _wrapped = wrapped;
            _moduleDescriptor = moduleDescriptor;
            _assemblyName = assemblyName;
            _assemblyVersion = assemblyVersion;
            _isComponentAssembly = isComponentAssembly;
            _forceLoad = forceLoad;
        }

        public IBlazorModuleDescriptor ModuleDescriptor => _moduleDescriptor ?? _wrapped.ModuleDescriptor;

        public string AssemblyName => _assemblyName ?? _wrapped.AssemblyName;

        public Version AssemblyVersion => _assemblyVersion ?? _wrapped.AssemblyVersion;

        public bool IsComponentAssembly => _isComponentAssembly ?? _wrapped.IsComponentAssembly;

        public ValueTask<BlazorModuleAssemblySource> LoadAssemblySourceAsync(CancellationToken cancellation = default)
        {
            if (_forceLoad is null)
            {
                return _wrapped.LoadAssemblySourceAsync(cancellation);
            }

            return InternalLoadAssemblySourceAsync(cancellation);
        }

        private async ValueTask<BlazorModuleAssemblySource> InternalLoadAssemblySourceAsync(CancellationToken cancellation)
        {
            Debug.Assert(_forceLoad != null);
            var source = await _wrapped.LoadAssemblySourceAsync(cancellation);
            return source.Configure(_forceLoad.Value);
        }
    }
}
