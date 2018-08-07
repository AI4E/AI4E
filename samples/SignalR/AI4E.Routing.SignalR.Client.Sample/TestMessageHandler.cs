using System;
using System.Threading.Tasks;
using AI4E.Routing.SignalR.Sample.Common;

namespace AI4E.Routing.SignalR.Client.Sample
{
    public sealed class TestMessageHandler
    {
        public async Task HandleAsync(TestMessage message)
        {
            await Console.Out.WriteLineAsync("Received message: " + message.Message);
        }
    }
}
