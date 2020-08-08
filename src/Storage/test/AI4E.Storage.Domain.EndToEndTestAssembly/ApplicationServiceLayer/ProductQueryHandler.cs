using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Storage.Domain.EndToEndTestAssembly.Models;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.ApplicationServiceLayer
{
#pragma warning disable CA1822
    public sealed class ProductQueryHandler : MessageHandler
    {
        public ProductQueryHandler(IEntityStorage entityStorage, IEntityMetadataManager metadataManager)
        {
            if (entityStorage is null)
                throw new ArgumentNullException(nameof(entityStorage));

            if (metadataManager is null)
                throw new ArgumentNullException(nameof(metadataManager));

            EntityStorage = entityStorage;
            MetadataManager = metadataManager;
        }

        public IEntityStorage EntityStorage { get; }
        public IEntityMetadataManager MetadataManager { get; }

        public async ValueTask<ProductModel?> HandleAsync(ByIdQuery<ProductModel> query, CancellationToken cancellation)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            var productId = query.Id;
            var productLoadResult = await EntityStorage.LoadEntityAsync( // TODO: Generic API
                new EntityIdentifier(typeof(Product), productId.ToString()), cancellation).ConfigureAwait(false);
            var product = productLoadResult.GetEntity(throwOnFailure: false) as Product; // TODO: Generic API

            return Project(product);
        }

        [return: NotNullIfNotNull("product")]
        private ProductModel? Project(Product? product)
        {
            if (product is null)
                return null;

            return new ProductModel
            {
                Id = product.Id,
                ConcurrencyToken = MetadataManager.GetConcurrencyToken(new EntityDescriptor(typeof(Product), product)).RawValue, // TODO: Simplify this
                Name = product.Name,
                Price = product.Price
            };
        }

        public IAsyncEnumerable<ProductListModel> HandleAsync(
            Query<ProductListModel> query, CancellationToken cancellation)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            var productLoadResults = EntityStorage.LoadEntitiesAsync(typeof(Product), cancellation); // TODO: Generic API
            var products = productLoadResults.Select(p => p.Entity).Cast<Product>(); // TODO: Generic API
            return products.Select(p => ProjectToListModel(p));
        }

        [return: NotNullIfNotNull("product")]
        private ProductListModel? ProjectToListModel(Product? product)
        {
            if (product is null)
                return null;

            return new ProductListModel
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price
            };
        }

        public ValueTask<ProjectedProductModel?> HandleAsync(
            ByIdQuery<ProjectedProductModel> query,
            IDatabase database,
            CancellationToken cancellation)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            if (database is null)
                throw new ArgumentNullException(nameof(database));

            var productId = query.Id;
            return database.GetOneAsync<ProjectedProductModel>(q => q.Id == productId, cancellation);
        }

        public IAsyncEnumerable<ProjectedProductListModel> HandleAsync(
           Query<ProjectedProductListModel> query,
           IDatabase database,
           CancellationToken cancellation)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            if (database is null)
                throw new ArgumentNullException(nameof(database));

            return database.GetAsync<ProjectedProductListModel>(cancellation);
        }
    }
#pragma warning restore CA1822
}
