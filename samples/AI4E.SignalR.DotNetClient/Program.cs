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
            var connection = new HubConnectionBuilder().WithUrl("http://localhost:64195/MessageDispatcherHub").Build();
            var frontEndMessageDispatcher = new FrontEndMessageDispatcher(connection);          
            await connection.StartAsync();
           
            TestSignalRCommand command = new TestSignalRCommand("this was sent via SignalR");
            var result = await frontEndMessageDispatcher.DispatchAsync(command, new DispatchValueDictionary(), default);
            Console.WriteLine(result.ToString());
            Console.ReadLine();
        }     
    }
}
