using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleSourceQueryHandler : MessageHandler
    {
        private readonly IDataStore _dataStore;

        public ModuleSourceQueryHandler(IDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public async Task<IEnumerable<ModuleSourceListModel>> HandleAsync(Query<ModuleSourceListModel> query)
        {
            return await _dataStore.AllAsync<ModuleSourceListModel>().ToArray();
        }

        public Task<ModuleSourceModel> HandleAsync(ByIdQuery<ModuleSourceModel> query)
        {
            return _dataStore.FindOneAsync<ModuleSourceModel>(p => p.Id == query.Id).AsTask();
        }

        public Task<ModuleSourceDeleteModel> HandleAsync(ByIdQuery<ModuleSourceDeleteModel> query)
        {
            return _dataStore.FindOneAsync<ModuleSourceDeleteModel>(p => p.Id == query.Id).AsTask();
        }
    }
}
