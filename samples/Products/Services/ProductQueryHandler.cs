using AI4E;
using AI4E.Storage;
using Products.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Products.Services
{
    public class ProductQueryHandler : MessageHandler
    {
        private readonly IDataStore _dataStore;

        public ProductQueryHandler(IDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public Task<ProductModel> HandleAsync(ByIdQuery<ProductModel> query)
        {
            return _dataStore.FindOneAsync<ProductModel>(p => p.Id == query.Id);
        }

        public Task<ProductRenameModel> HandleAsync(ByIdQuery<ProductRenameModel> query)
        {
            return _dataStore.FindOneAsync<ProductRenameModel>(p => p.Id == query.Id);
        }

        public Task<ProductChangePriceModel> HandleAsync(ByIdQuery<ProductChangePriceModel> query)
        {
            return _dataStore.FindOneAsync<ProductChangePriceModel>(p => p.Id == query.Id);
        }

        public Task<IEnumerable<ProductModel>> HandleAsync(Query<IEnumerable<ProductModel>> query)
        {
            return _dataStore.QueryAsync<ProductModel>(p => p);          
        }
    }
}
