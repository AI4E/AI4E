using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Hosting.Sample.Api;
using AI4E.Modularity.Hosting.Sample.Domain;
using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage;
using Microsoft.AspNetCore.Mvc;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleQueryHandler : MessageHandler
    {
        public async Task<IEnumerable<ModuleListModel>> HandleAsync(ModuleSearchQuery query, [FromServices] IModuleSearchEngine searchEngine)
        {
            var modules = await searchEngine.SearchModulesAsync(query.SearchPhrase, query.IncludePreReleases, cancellation: default);
            var projection = new ModuleProjection();

            return modules.Select(p => projection.ProjectToListModel(p, query.IncludePreReleases));
        }

        public async Task<ModuleReleaseModel> HandleAsync(ByIdQuery<ModuleReleaseIdentifier, ModuleReleaseModel> query, [FromServices] IDataStore dataStore)
        {
            var a = await dataStore.AllAsync<ModuleReleaseModel>().ToArray();

            try
            {
                var result = await dataStore.FindOneAsync<ModuleReleaseModel>(p => p.Id == query.Id).AsTask();

                return result;
            }
            catch (Exception exc)
            {
                throw;
            }
        }
    }
}
