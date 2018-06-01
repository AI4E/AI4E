using System;
using System.Collections.Generic;
using System.Text;

namespace AI4E.SignalR.DotNetClient.Api
{
    public sealed class TestSignalRCommand
    {
        public TestSignalRCommand(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }
}
