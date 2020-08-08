using System;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.Models
{
    public sealed class ProductDeleted
    {
        public ProductDeleted(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }
    }
}
