using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Transactions;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Projection
{
    public sealed partial class ProjectionEngine : IProjectionEngine
    {
        private readonly IProjector _projector;
        private readonly IDatabase _database;
        private readonly ITransactionalDatabase _transactionalDatabase;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectionEngine> _logger;

        public ProjectionEngine(IProjector projector,
                                IDatabase database,
                                ITransactionalDatabase transactionalDatabase,
                                IServiceProvider serviceProvider,
                                ILogger<ProjectionEngine> logger = default)
        {
            if (projector == null)
                throw new ArgumentNullException(nameof(projector));

            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (transactionalDatabase == null)
                throw new ArgumentNullException(nameof(transactionalDatabase));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _projector = projector;
            _database = database;
            _transactionalDatabase = transactionalDatabase;
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

            var scopedEngine = new SourceScopedProjectionEngine(entityDescriptor, _projector, _transactionalDatabase, _database, _serviceProvider);
            var dependents = await scopedEngine.ProjectAsync(cancellation);

            processedEntities.Add(entityDescriptor);

            foreach (var dependent in dependents)
            {
                await ProjectAsync(dependent, processedEntities, cancellation);
            }
        }
    }
}
