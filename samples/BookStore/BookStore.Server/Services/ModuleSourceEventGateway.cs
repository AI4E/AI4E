using System.Threading;
using System.Threading.Tasks;
using AI4E;
using AI4E.Modularity.Host;
using BookStore.Events;

namespace BookStore.Server.Services
{
    public sealed class ModuleSourceEventGateway : MessageHandler
    {
        public ValueTask<IDispatchResult> HandleAsync(
            ModuleSourceAdded @event, CancellationToken cancellation = default)
        {
            var integrationEvent = new ModuleSourceAddedEvent(
                @event.ModuleSourceId, @event.Location);

            return MessageDispatcher.DispatchAsync(
                integrationEvent, publish: true, cancellation);
        }

        public ValueTask<IDispatchResult> HandleAsync(
            ModuleSourceRemoved @event, CancellationToken cancellation = default)
        {
            var integrationEvent = new ModuleSourceRemovedEvent(
                @event.ModuleSourceId);

            return MessageDispatcher.DispatchAsync(
                integrationEvent, publish: true, cancellation);
        }
    }
}
