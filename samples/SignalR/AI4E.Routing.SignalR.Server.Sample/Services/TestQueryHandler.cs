using System;
using AI4E.Routing.SignalR.Sample.Common;

namespace AI4E.Routing.SignalR.Server.Sample.Services
{
    public sealed class TestQueryHandler : MessageHandler
    {
        public DateTime Handle(TestQuery query)
        {
            return DateTime.Now;
        }
    }
}
