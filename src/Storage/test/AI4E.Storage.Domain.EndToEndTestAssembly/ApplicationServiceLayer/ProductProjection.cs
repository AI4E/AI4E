using System;
using AI4E.Storage.Domain.EndToEndTestAssembly.Models;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.ApplicationServiceLayer
{
#pragma warning disable CA1822
    public sealed class ProductProjection
    {
        public ProductProjection(IEntityMetadataManager metadataManager)
        {
            if (metadataManager is null)
                throw new ArgumentNullException(nameof(metadataManager));

            MetadataManager = metadataManager;
        }

        public IEntityMetadataManager MetadataManager { get; }

        public ProjectedProductModel Project(Product product)
        {
            if (product is null)
                throw new ArgumentNullException(nameof(product));

            return new ProjectedProductModel
            {
                Id = product.Id,
                ConcurrencyToken = MetadataManager.GetConcurrencyToken(new EntityDescriptor(typeof(Product), product)).RawValue, // TODO: Simplify this
                Name = product.Name,
                Price = product.Price
            };
        }

        public ProjectedProductListModel ProjectToListModel(Product product)
        {
            if (product is null)
                throw new ArgumentNullException(nameof(product));

            return new ProjectedProductListModel
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price
            };
        }
    }
#pragma warning restore CA1822
}
