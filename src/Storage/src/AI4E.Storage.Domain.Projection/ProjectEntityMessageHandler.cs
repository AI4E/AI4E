using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Messaging.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain.Projection
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
            RouteEndPointScope remoteScope,
            CancellationToken cancellation)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            var entityType = dispatchData.Message.EntityType;
            var entityId = dispatchData.Message.EntityId;

            await _projectionEngine.ProjectAsync(entityType, entityId, cancellation).ConfigureAwait(false);
            return new SuccessDispatchResult();
        }

        public static IProjectionBuilder Register(IProjectionBuilder projectionBuilder)
        {
            if (projectionBuilder is null)
                throw new ArgumentNullException(nameof(projectionBuilder));

            return projectionBuilder.ConfigureServices(ConfigureServices);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddMessaging().ConfigureMessageHandlers(ConfigureMessageHandlers);
        }

        private static void ConfigureMessageHandlers(IMessageHandlerRegistry registry, IServiceProvider serviceProvider)
        {
            registry.Register(new MessageHandlerRegistration<ProjectEntityMessage>(
                    serviceProvider => ActivatorUtilities.CreateInstance<ProjectEntityMessageHandler>(
                        serviceProvider)));
        }
    }
}
