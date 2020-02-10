using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    internal sealed class DomainStorageBuilder : IDomainStorageBuilder
    {
        public DomainStorageBuilder(IStorageBuilder storageBuilder)
        {
            Debug.Assert(storageBuilder != null);
            StorageBuilder = storageBuilder;
        }

        public IServiceCollection Services => StorageBuilder.Services;

        public IStorageBuilder StorageBuilder { get; }
    }
}
