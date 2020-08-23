using System.Reflection;
using AI4E.Messaging;
using AI4E.Storage.MongoDB.Test.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain.EndToEndTestAssembly
{
    public sealed class Setup
    {
        public bool ActivateProjections { get; }

        public Setup(bool activateProjections = false)
        {
            ActivateProjections = activateProjections;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureMessaging(services.AddMessaging()); // TODO: API
            services.AddStorage(ConfigureStorage);

            services.ConfigureAssemblyRegistry(registry =>
            {
                registry.AddAssembly(Assembly.GetExecutingAssembly());
            });
        }

        private void ConfigureMessaging(IMessagingBuilder messagingBuilder)
        {
            messagingBuilder.UseValidation();
        }

        private void ConfigureStorage(IStorageBuilder storageBuilder)
        {
            // Use MongoDb as database engine
            storageBuilder.UseMongoDB(options =>
            {
                options.ConnectionString = DatabaseRunner.GetConnectionString();
                options.Database = DatabaseName.GenerateRandom();
            });

            // Add the domain storage services
            storageBuilder.AddDomainStorage(ConfigureDomainStorage);
        }

        private void ConfigureDomainStorage(IDomainStorageBuilder domainStorage)
        {
            domainStorage.Configure(options =>
            {
                options.SynchronousEventDispatch = true;
            });

            if (ActivateProjections)
            {
                domainStorage.AddProjection();
            }
        }
    }
}
