using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Hosting.Sample.Api;
using AI4E.Modularity.Hosting.Sample.Domain;
using AI4E.Modularity.Hosting.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleQueryHandler : MessageHandler
    {
        public async Task<IEnumerable<ModuleListModel>> HandleAsync(ModuleSearchQuery query, [FromServices] IModuleSearchEngine searchEngine)
        {
            var modules = await searchEngine.SearchModulesAsync(query.SearchPhrase, query.IncludePreReleases, cancellation: default);
            var projection = new ModuleProjection();

            return modules.Select(p => projection.ProjectToListModel(p));
        }
    }
}
