using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleSourceQueryHandler : MessageHandler
    {
        private readonly IDatabase _dataStore;

        public ModuleSourceQueryHandler(IDatabase dataStore)
        {
            _dataStore = dataStore;
        }

        public async Task<IEnumerable<ModuleSourceListModel>> HandleAsync(Query<IEnumerable<ModuleSourceListModel>> query)
        {
            return await _dataStore.GetAsync<ModuleSourceListModel>().ToArray();
        }

        public Task<ModuleSourceModel> HandleAsync(ByIdQuery<ModuleSourceModel> query)
        {
            return _dataStore.GetOneAsync<ModuleSourceModel>(p => p.Id == query.Id).AsTask();
        }

        public Task<ModuleSourceDeleteModel> HandleAsync(ByIdQuery<ModuleSourceDeleteModel> query)
        {
            return _dataStore.GetOneAsync<ModuleSourceDeleteModel>(p => p.Id == query.Id).AsTask();
        }

        public Task<ModuleSourceRenameModel> HandleAsync(ByIdQuery<ModuleSourceRenameModel> query)
        {
            return _dataStore.GetOneAsync<ModuleSourceRenameModel>(p => p.Id == query.Id).AsTask();
        }

        public Task<ModuleSourceUpdateLocationModel> HandleAsync(ByIdQuery<ModuleSourceUpdateLocationModel> query)
        {
            return _dataStore.GetOneAsync<ModuleSourceUpdateLocationModel>(p => p.Id == query.Id).AsTask();
        }
    }
}
