using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.Transactions;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Projection
{
    public sealed partial class ProjectionEngine : IProjectionEngine
    {
        private readonly IProjector _projector;
        private readonly ITransactionalDatabase _database;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectionEngine> _logger;

        public ProjectionEngine(IProjector projector,
                                ITransactionalDatabase database,
                                IServiceProvider serviceProvider,
                                ILogger<ProjectionEngine> logger = default)
        {
            if (projector == null)
                throw new ArgumentNullException(nameof(projector));

            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _projector = projector;
            _database = database;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task ProjectAsync(Type entityType, string id, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            var processedEntities = new HashSet<ProjectionSourceDescriptor>();
            return ProjectAsync(new ProjectionSourceDescriptor(entityType, id), processedEntities, cancellation);
        }

        private async Task ProjectAsync(ProjectionSourceDescriptor entityDescriptor,
                                        ISet<ProjectionSourceDescriptor> processedEntities,
                                        CancellationToken cancellation)
        {
            if (processedEntities.Contains(entityDescriptor))
            {
                return;
            }

            IEnumerable<ProjectionSourceDescriptor> dependents;

            using (var database = _database.CreateScope())
            {
                do
                {
                    try
                    {
                        var scopedEngine = new SourceScopedProjectionEngine(entityDescriptor, _projector, database, _serviceProvider);
                        dependents = await scopedEngine.ProjectAsync(cancellation);

                        if (await database.TryCommitAsync(cancellation))
                        {
                            break;
                        }
                    }
                    catch (TransactionAbortedException) { }
                    catch
                    {
                        await database.RollbackAsync();
                        throw;
                    }
                }
                while (true);
            }

            processedEntities.Add(entityDescriptor);

            foreach (var dependent in dependents)
            {
                await ProjectAsync(dependent, processedEntities, cancellation);
            }
        }
    }
}
