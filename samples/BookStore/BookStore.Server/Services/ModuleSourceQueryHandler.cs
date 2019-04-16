using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using AI4E.Storage;
using BookStore.Models;

namespace BookStore.Server.Services
{
    public sealed class ModuleSourceQueryHandler : MessageHandler
    {
        private readonly IDatabase _database;

        public ModuleSourceQueryHandler(IDatabase database)
        {
            _database = database;
        }

        public async Task<IEnumerable<ModuleSourceListModel>> HandleAsync(
            Query<IEnumerable<ModuleSourceListModel>> query,
            CancellationToken cancellation = default)
        {
            // TODO: https://github.com/AI4E/AI4E/issues/144
            return await _database.GetAsync<ModuleSourceListModel>(cancellation).ToArray(cancellation);
        }

        public ValueTask<ModuleSourceModel> HandleAsync(
            ByIdQuery<ModuleSourceModel> query, CancellationToken cancellation = default)
        {
            return _database.GetOneAsync<ModuleSourceModel>(p => p.Id == query.Id, cancellation);
        }

        public ValueTask<ModuleSourceDeleteModel> HandleAsync(
            ByIdQuery<ModuleSourceDeleteModel> query, CancellationToken cancellation = default)
        {
            return _database.GetOneAsync<ModuleSourceDeleteModel>(p => p.Id == query.Id, cancellation);
        }
    }
}
