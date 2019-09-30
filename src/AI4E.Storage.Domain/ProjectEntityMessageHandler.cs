using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Storage.Projection;

namespace AI4E.Storage.Domain
{
    internal sealed class ProjectEntityMessageHandler : IMessageHandler<ProjectEntityMessage>
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
            var entityType = dispatchData.Message.EntityType;
            var entityId = dispatchData.Message.EntityId;

            await _projectionEngine.ProjectAsync(entityType, entityId, cancellation);
            return new SuccessDispatchResult();
        }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            if (!(dispatchData.Message is ProjectEntityMessage))
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{typeof(ProjectEntityMessage)}'.");

            if (!(dispatchData is DispatchDataDictionary<ProjectEntityMessage> typedDispatchData))
            {
                typedDispatchData = new DispatchDataDictionary<ProjectEntityMessage>(dispatchData.Message as ProjectEntityMessage, dispatchData);
            }

            return HandleAsync(typedDispatchData, publish, localDispatch, cancellation);
        }

        public Type MessageType => typeof(ProjectEntityMessage);
#endif
    }

    internal sealed class ProjectEntityMessage
    {
        public ProjectEntityMessage(Type entityType, string entityId)
        {
            EntityType = entityType;
            EntityId = entityId;
        }

        public Type EntityType { get; }
        public string EntityId { get; }
    }
}
