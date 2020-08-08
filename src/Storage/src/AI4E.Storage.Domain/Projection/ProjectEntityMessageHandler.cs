using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Storage.Domain.Projection;

namespace AI4E.Storage.Domain
{
    public sealed class ProjectEntityMessageHandler : IMessageHandler<ProjectEntityMessage>
    {
        private readonly IProjectionEngine _projectionEngine;

        public ProjectEntityMessageHandler(IProjectionEngine projectionEngine)
        {
            if (projectionEngine is null)
                throw new ArgumentNullException(nameof(projectionEngine));

            _projectionEngine = projectionEngine;
        }

        public async ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<ProjectEntityMessage> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            var entityType = dispatchData.Message.EntityType;
            var entityId = dispatchData.Message.EntityId;

            await _projectionEngine.ProjectAsync(entityType, entityId, cancellation).ConfigureAwait(false);
            return new SuccessDispatchResult();
        }
    }
}
