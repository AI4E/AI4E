using System.Reflection;
using System.Threading.Tasks;
using AI4E.Utils.ApplicationParts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Handler
{
    [TestClass]
    public class Issue146
    {
        [TestMethod]
        public void IsMessageHandlerTest()
        {
            var provider = new MessageHandlerFeatureProvider();

            Assert.IsTrue(provider.IsMessageHandler(typeof(ContentObjectEventGateway)));
        }

        [TestMethod]
        public void PopulateFeatureTest()
        {
            var appParts = new[] { new AssemblyPart(Assembly.GetExecutingAssembly()) };

            var feature = new MessageHandlerFeature();
            var provider = new MessageHandlerFeatureProvider();

            provider.PopulateFeature(appParts, feature);

            Assert.IsTrue(feature.MessageHandlers.Contains(typeof(ContentObjectEventGateway)));
        }
    }

    public sealed class ContentObjectEventGateway : MessageHandler
    {
#pragma warning disable CS1998, IDE0060
        public async ValueTask<IDispatchResult> HandleAsync(ContentObjectCreated @event)
#pragma warning restore CS1998, IDE0060 
        {
            return default;
        }
    }

    public class ContentObjectCreated { }
}

