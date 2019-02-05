using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using Nito.AsyncEx;

namespace AI4E.Blazor.Modularity
{
    public abstract class InstallationSetManager : IInstallationSetManager
    {
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly ISet<ModuleIdentifier> _inclusiveModules = new HashSet<ModuleIdentifier>();
        private readonly ISet<ModuleIdentifier> _exclusiveModules = new HashSet<ModuleIdentifier>();
        private ImmutableList<ModuleIdentifier> _installationSet = ImmutableList<ModuleIdentifier>.Empty;

        public event EventHandler InstallationSetChanged;

        protected IEnumerable<ModuleIdentifier> InstallationSet => _installationSet.Except(_exclusiveModules).Concat(_inclusiveModules);

        public async Task UpdateInstallationSetAsync(IEnumerable<ModuleIdentifier> installationSet, CancellationToken cancellation)
        {
            if (installationSet == null)
                throw new ArgumentNullException(nameof(installationSet));

            if (installationSet.Any(p => p == default))
                throw new ArgumentException("The collection must not contain default values.", nameof(installationSet));

            using (await _lock.LockAsync(cancellation))
            {
                _installationSet = installationSet.ToImmutableList();
                await UpdateInternalAsync(cancellation);
            }
        }

        public async Task InstallAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            using (await _lock.LockAsync(cancellation))
            {
                _exclusiveModules.Remove(module);
                _inclusiveModules.Add(module);
                await UpdateInternalAsync(cancellation);
            }
        }

        public async Task UninstallAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            using (await _lock.LockAsync(cancellation))
            {
                _inclusiveModules.Remove(module);
                _exclusiveModules.Add(module);
                await UpdateInternalAsync(cancellation);
            }
        }

        private async Task UpdateInternalAsync(CancellationToken cancellation)
        {
            if (await UpdateAsync(cancellation))
            {
                InstallationSetChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected abstract Task<bool> UpdateAsync(CancellationToken cancellation);
    }
}
