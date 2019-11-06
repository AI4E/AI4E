namespace Routing.Modularity.Sample.PluginA.Services
{
    public sealed class TestMessageHandler
    {
#pragma warning disable CA1822
        public TestMessageResult Handle(TestMessage message)
#pragma warning restore CA1822
        {
#pragma warning disable CA1062 
            return new TestMessageResult(message.Str);
#pragma warning restore CA1062
        }
    }
}
