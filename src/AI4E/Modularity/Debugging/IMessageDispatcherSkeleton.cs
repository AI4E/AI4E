using System;
using System.Threading.Tasks;
using AI4E.Modularity.RPC;

namespace AI4E.Modularity.Debugging
{
    public interface IMessageDispatcherSkeleton
    {
        Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, EndPointRoute endPoint);

        Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, EndPointRoute endPoint);

        Task<IDispatchResult> DispatchLocalAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish);

        Task<IDispatchResult> DispatchLocalAsync(Type messageType, object message, DispatchValueDictionary context, bool publish);

        Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish);

        Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish);

        Proxy<IHandlerRegistration<IMessageHandler<TMessage>>> Register<TMessage>(Proxy<IProvider<Proxy<IMessageHandler<TMessage>>>> proxy);

    }
}
