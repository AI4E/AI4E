namespace Routing.Modularity.Sample.PluginA
{
    public sealed class TestMessage
    {
        public TestMessage(string str)
        {
            Str = str;
        }

        public string Str { get; }
    }
}
