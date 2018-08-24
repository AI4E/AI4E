using System;
using System.Threading.Tasks;
using AI4E.Storage.Domain;

namespace AI4E.Modularity.Host
{
    public sealed class ResolvedInstallationSetQueryHandler : MessageHandler
    {
        private readonly IEntityStorageEngine _storageEngine;

        public ResolvedInstallationSetQueryHandler(IEntityStorageEngine storageEngine)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            _storageEngine = storageEngine;
        }

        public async Task<ResolvedInstallationSet> HandleAsync(Query<ResolvedInstallationSet> query)
        {
            var config = await _storageEngine.GetByIdAsync<ModuleInstallationConfiguration>(default(SingletonId).ToString(), cancellation: default);

            return config?.ResolvedModules ?? default;
        }
    }
}
