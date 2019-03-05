using System;

namespace AI4E.Routing.SignalR.Server
{
    public class ClientConnectionOptions
    {
        public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromMinutes(10);

        public TimeSpan Timeout { get; set; } = DefaultTimeout;
    }
}
