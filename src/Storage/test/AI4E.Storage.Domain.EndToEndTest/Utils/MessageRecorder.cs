using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain.EndToEndTest.Utils
{
    public sealed class MessageRecorder<T> : IMessageHandler<T>
           where T : class
    {
        private readonly ImmutableList<DispatchDataDictionary<T>>.Builder _recordedMessages
            = ImmutableList.CreateBuilder<DispatchDataDictionary<T>>();
        private readonly object _mutex = new object();
        private bool _dirty = false;
        private ImmutableList<DispatchDataDictionary<T>> _fixed = ImmutableList<DispatchDataDictionary<T>>.Empty;

        public ImmutableList<DispatchDataDictionary<T>> RecordedMessages
        {
            get
            {
                var result = Volatile.Read(ref _fixed);

                if (!Volatile.Read(ref _dirty))
                {
                    return result;
                }

                lock (_mutex)
                {
                    if (!_dirty)
                    {
                        return _fixed;
                    }

                    _fixed = _recordedMessages.ToImmutable();
                    _dirty = false;

                    return _fixed;
                }
            }
        }

        public void Clear()
        {
            lock (_mutex)
            {
                _recordedMessages.Clear();
                _fixed = ImmutableList<DispatchDataDictionary<T>>.Empty;
                _dirty = false;
            }
        }

        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<T> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            if (!publish)
                return new ValueTask<IDispatchResult>(new DispatchFailureDispatchResult(dispatchData.MessageType));

            lock (_mutex)
            {
                _recordedMessages.Add(dispatchData);
                _dirty = true;
            }

            return new ValueTask<IDispatchResult>(new SuccessDispatchResult());
        }

        internal static void Configure(IMessageHandlerRegistry registry, IServiceProvider serviceProvider)
        {
            registry.Register(new MessageHandlerRegistration<T>(Factory)); // TODO: Only register for publish-subscribe
        }

        private static IMessageHandler<T> Factory(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<MessageRecorder<T>>();
        }
    }

    public static class DomainEndToEndTestServiceCollectionExtension
    {
        public static IServiceCollection AddMessageRecorder<T>(this IServiceCollection serviceCollection)
            where T : class
        {
            if (serviceCollection is null)
                throw new ArgumentNullException(nameof(serviceCollection));

            serviceCollection.AddSingleton<MessageRecorder<T>>();
            serviceCollection.AddMessaging().ConfigureMessageHandlers(MessageRecorder<T>.Configure);

            return serviceCollection;
        }
    }
}
