using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Host;
using AI4E.Modularity.Hosting.Sample.Api;
using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage;
using Microsoft.AspNetCore.Mvc;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleQueryHandler : MessageHandler
    {
        public async Task<IEnumerable<ModuleListModel>> HandleAsync(ModuleSearchQuery query, IModuleSearchEngine searchEngine)
        {
            var modules = await searchEngine.SearchModulesAsync(query.SearchPhrase, query.IncludePreReleases, cancellation: default);
            var projection = new ModuleProjection();

            return modules.Select(p => projection.ProjectToListModel(p, query.IncludePreReleases)).ToList();
        }

        public Task<ModuleReleaseModel> HandleAsync(ByIdQuery<ModuleReleaseIdentifier, ModuleReleaseModel> query, IDataStore dataStore)
        {
            return dataStore.FindOneAsync<ModuleReleaseModel>(p => p.Id == query.Id).AsTask();
        }

        public Task<ModuleInstallModel> HandleAsync(ByIdQuery<ModuleReleaseIdentifier, ModuleInstallModel> query, IDataStore dataStore)
        {
            return dataStore.FindOneAsync<ModuleInstallModel>(p => p.Id == query.Id).AsTask();
        }

        public Task<ModuleUninstallModel> HandleAsync(ByIdQuery<ModuleIdentifier, ModuleUninstallModel> query, IDataStore dataStore)
        {
            return dataStore.FindOneAsync<ModuleUninstallModel>(p => p.Id == query.Id).AsTask();
        }
    }
}
