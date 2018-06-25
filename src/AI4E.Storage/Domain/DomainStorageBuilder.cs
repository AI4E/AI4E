using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    internal sealed class DomainStorageBuilder : IDomainStorageBuilder
    {
        private readonly IStorageBuilder _storageBuilder;

        public DomainStorageBuilder(IStorageBuilder storageBuilder)
        {
            Assert(storageBuilder != null);
            _storageBuilder = storageBuilder;
        }

        public IServiceCollection Services => _storageBuilder.Services;
    }
}
