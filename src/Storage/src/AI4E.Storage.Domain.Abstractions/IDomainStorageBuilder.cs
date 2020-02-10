using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    public interface IDomainStorageBuilder
    {
        IStorageBuilder StorageBuilder { get; }

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        public IServiceCollection Services => StorageBuilder.Services;
#else
        IServiceCollection Services { get; }
#endif
    }
}
