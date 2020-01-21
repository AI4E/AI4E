namespace Routing.Modularity.Sample.PluginA.Services
{
    public sealed class HostMessageHandler
    {
#pragma warning disable CA1822
        public HostMessageResult Handle(HostMessage message)
#pragma warning restore CA1822
        {
#pragma warning disable CA1062 
            return new HostMessageResult(message.Str);
#pragma warning restore CA1062
        }
    }
}
