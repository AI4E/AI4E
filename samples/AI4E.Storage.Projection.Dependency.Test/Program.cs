using System;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Domain.Services;
using AI4E.Storage.Domain;
using AI4E.Storage.InMemory;
using AI4E.Storage.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace AI4E.Storage.Projection.Dependency.Test
{
    class Program
    {
        private static IServiceProvider ServiceProvider { get; set; }

        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            await RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddInMemoryMessaging();

            services.AddStorage()
                    .UseMongoDB(options =>
                    {
                        options.ConnectionString = "mongodb://localhost:27017";
                        options.Database = "AI4EDependencyTestDB";
                    })
                    //.UseInMemoryDatabase()
                    .UseDomainStorage();

            services.AddDomainServices();
        }

        private static async Task RunAsync()
        {
            var dependencyId = Guid.NewGuid();
            var dependentId = Guid.NewGuid();

            using (var scope = ServiceProvider.CreateScope())
            {
                var scopedServiceProvider = scope.ServiceProvider;
                var storageEngine = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();

                var dependency = new Dependency(dependencyId, "abc");

                await storageEngine.StoreAsync(typeof(Dependency), dependency, dependencyId.ToString());
                await storageEngine.StoreAsync(typeof(Dependent), new Dependent(dependentId, dependency), dependentId.ToString());
            }

            var model = await LoadModelUncachedAsync(dependentId);

            await Console.Out.WriteLineAsync(model.DependencyName);

            using (var scope = ServiceProvider.CreateScope())
            {
                var scopedServiceProvider = scope.ServiceProvider;
                var storageEngine = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();

                var dependency = (Dependency)await storageEngine.GetByIdAsync(typeof(Dependency), dependencyId.ToString());

                dependency.Name = "def";

                await storageEngine.StoreAsync(typeof(Dependency), dependency, dependencyId.ToString());
            }

            model = await LoadModelUncachedAsync(dependentId);

            await Console.Out.WriteLineAsync(model.DependencyName);
            await Console.In.ReadLineAsync();
        }

        private static async Task<DependentModel> LoadModelUncachedAsync(Guid id)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var dataStore = scope.ServiceProvider.GetRequiredService<IDataStore>();
                return await dataStore.FindOneAsync<DependentModel>(p => p.Id == id);
            }
        }
    }

    public sealed class Dependency : AggregateRoot
    {
        public Dependency(Guid id, string name) : base(id)
        {
            Name = name;
        }

        public string Name { get; set; }
    }

    public sealed class Dependent : AggregateRoot
    {
        public Dependent(Guid id, Dependency dependency) : base(id)
        {
            if (dependency == null)
                throw new ArgumentNullException(nameof(dependency));

            Dependency = dependency;
        }

        [JsonConstructor]
        private Dependent(Guid id, Reference<Dependency> dependency) : base(id)
        {
            if (dependency == null)
                throw new ArgumentNullException(nameof(dependency));

            Dependency = dependency;
        }

        public Reference<Dependency> Dependency { get; }
    }

    public sealed class DependentModel
    {
        public Guid Id { get; set; }
        public string DependencyName { get; set; }
    }

    public sealed class DependentProjection
    {
        public async Task<DependentModel> ProjectAsync(Dependent dependent)
        {
            var dependency = await dependent.Dependency;

            return new DependentModel
            {
                Id = dependent.Id,
                DependencyName = dependency.Name
            };
        }
    }
}

