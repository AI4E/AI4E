using AI4E.SignalR.DotNetClient.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI4E.SignalR.Server.Services
{
    public class TestSignalRCommandHandler : MessageHandler
    {
        public IDispatchResult Handle(TestSignalRCommand command)
        {
            return Success(" got handled");
        }
    }
}
