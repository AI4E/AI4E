using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public sealed class Issue241
    {
        private static IMessageDispatcher BuildMessageDispatcher(ObjectHandler handler)
        {
            return MessagingBuilder.CreateDefault().ConfigureMessageHandlers((registry, serviceProvider) =>
            {
                registry.Register(new MessageHandlerRegistration<object>(serviceProvider => handler));
            }).BuildMessageDispatcher();
        }

        [TestMethod]
        public async Task Test()
        {
            var handler = new ObjectHandler();
            var messageDispatcher = BuildMessageDispatcher(handler);

            await messageDispatcher.DispatchAsync(new DispatchDataDictionary<string>("abc"), cancellation: default);

            Assert.AreEqual(typeof(string), handler.DispatchData.MessageType);
        }

        private sealed class ObjectHandler : IMessageHandler<object>
        {
            public DispatchDataDictionary<object> DispatchData { get; set; }

            public ValueTask<IDispatchResult> HandleAsync(
                DispatchDataDictionary<object> dispatchData,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                DispatchData = dispatchData;
                return new ValueTask<IDispatchResult>(new SuccessDispatchResult());
            }
        }
    }
}
