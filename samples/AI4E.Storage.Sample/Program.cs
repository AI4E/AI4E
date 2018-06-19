using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Storage.Domain;
using AI4E.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Sample
{
    public static class Program
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
                    .UseInMemoryDatabase()
                    .UseDomainStorage();
        }

        private static async Task RunAsync()
        {
            var id = Guid.NewGuid().ToString();
            var dependentId = Guid.NewGuid().ToString();

            using (var entityStorageEngine = ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var entity = new TestEntity(id)
                {
                    Value = "abc"
                };

                await entityStorageEngine.StoreAsync(typeof(TestEntity), entity, id);
            }

            using (var dataStore = ServiceProvider.GetRequiredService<IDataStore>())
            {
                var model = await dataStore.FindOneAsync<TestEntityModel>(p => p.Id == id);

                await Console.Out.WriteLineAsync(model.Value);
                await Console.Out.WriteLineAsync(model.ConcurrencyToken);

                await Console.Out.WriteLineAsync();
            }

            using (var entityStorageEngine = ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var entity = new DependentEntity(dependentId)
                {
                    DependencyId = id
                };

                await entityStorageEngine.StoreAsync(typeof(DependentEntity), entity, dependentId);
            }

            using (var dataStore = ServiceProvider.GetRequiredService<IDataStore>())
            {
                var model = await dataStore.FindOneAsync<DependentEntityModel>(p => p.Id == dependentId);

                await Console.Out.WriteLineAsync(model.DependencyValue);

                await Console.Out.WriteLineAsync();
            }

            using (var entityStorageEngine = ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var entity = (TestEntity)await entityStorageEngine.GetByIdAsync(typeof(TestEntity), id);

                entity.Value = "def";

                await entityStorageEngine.StoreAsync(typeof(TestEntity), entity, id);
            }

            using (var dataStore = ServiceProvider.GetRequiredService<IDataStore>())
            {
                var model = await dataStore.FindOneAsync<DependentEntityModel>(p => p.Id == dependentId);

                await Console.Out.WriteLineAsync(model.DependencyValue);

                await Console.Out.WriteLineAsync();
            }

            using (var entityStorageEngine = ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var entity = (TestEntity)await entityStorageEngine.GetByIdAsync(typeof(TestEntity), id);

                var child1 = new ChildEntity(Guid.NewGuid().ToString());
                var child2 = new ChildEntity(Guid.NewGuid().ToString());

                await entityStorageEngine.StoreAsync(typeof(ChildEntity), child1, child1.Id);
                await entityStorageEngine.StoreAsync(typeof(ChildEntity), child2, child2.Id);

                entity.AddChild(child1);
                entity.AddChild(child2);

                await entityStorageEngine.StoreAsync(typeof(TestEntity), entity, id);
            }

            using (var dataStore = ServiceProvider.GetRequiredService<IDataStore>())
            {
                var models = await dataStore.FindAsync<TestEntityChildRelationshipModel>(p => p.ParentId == id).ToList();

                foreach (var model in models)
                {
                    await Console.Out.WriteLineAsync(model.ChildId);
                }

                await Console.Out.WriteLineAsync();
            }

            using (var entityStorageEngine = ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var entity = (TestEntity)await entityStorageEngine.GetByIdAsync(typeof(TestEntity), id);
                var child = (ChildEntity)await entityStorageEngine.GetByIdAsync(typeof(ChildEntity), entity.ChildIds.First());

                entity.RemoveChild(child);

                await entityStorageEngine.StoreAsync(typeof(TestEntity), entity, id);
            }

            using (var dataStore = ServiceProvider.GetRequiredService<IDataStore>())
            {
                var models = await dataStore.FindAsync<TestEntityChildRelationshipModel>(p => p.ParentId == id).ToList();

                foreach (var model in models)
                {
                    await Console.Out.WriteLineAsync(model.ChildId);
                }

                await Console.Out.WriteLineAsync();
            }

            await Console.In.ReadLineAsync();
        }
    }
}
