using AI4E.SignalR.DotNetClient.Api;
using AI4E.SignalR.DotNetClient.Routing;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AI4E.SignalR.DotNetClient
{
    class Program
    {
       
        static async Task Main(string[] args)
        {
            var connection = new HubConnectionBuilder().WithUrl("http://localhost:5002/MessageDispatcherHub").Build();
            var frontEndMessageDispatcher = new FrontEndMessageDispatcher(connection);
            connection.On<int, IDispatchResult>("GetDispatchResult", (seqNum, dispatchResult) =>
            {
                if (frontEndMessageDispatcher._responseTable.TryGetValue(seqNum, out TaskCompletionSource<IDispatchResult> tcs))
                {
                    if (tcs.TrySetResult(dispatchResult))
                    {
                        frontEndMessageDispatcher._responseTable.TryRemove(seqNum, out TaskCompletionSource<IDispatchResult> t);
                    }
                    else
                        return;
                }
                else
                {
                    return;
                }

            });
            await connection.StartAsync();

            
            TestSignalRCommand command = new TestSignalRCommand("this was sent via SignalR");
            var result = await frontEndMessageDispatcher.DispatchAsync(typeof(TestSignalRCommand), command, null, default);
            Console.WriteLine(result);
            Console.ReadLine();
        }     
    }
}
