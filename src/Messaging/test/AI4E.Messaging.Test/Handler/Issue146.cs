using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.MessageHandlers
{
    [TestClass]
    public class Issue146
    {
        [TestMethod]
        public void IsMessageHandlerTest()
        {
            Assert.IsTrue(MessageHandlerResolver.IsMessageHandler(typeof(ContentObjectEventGateway)));
        }

        [TestMethod]
        public void PopulateFeatureTest()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var provider = new MessageHandlerResolver();

            var messageHandlers = provider.ResolveMessageHandlers(assembly, default);

            Assert.IsTrue(messageHandlers.Contains(typeof(ContentObjectEventGateway)));
        }
    }

    public sealed class ContentObjectEventGateway : MessageHandler
    {
#pragma warning disable CS1998
        public async ValueTask<IDispatchResult> HandleAsync(ContentObjectCreated @event)
#pragma warning restore CS1998
        {
            return default;
        }
    }

    public class ContentObjectCreated { }
}

