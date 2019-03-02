using System;

namespace AI4E.Routing.SignalR.Server
{
    public class ClientConnectionManagerOptions
    {
        public static TimeSpan DefaultGarbageCollectionDelayMax { get; } = TimeSpan.FromSeconds(10);
        public static string DefaultBasePath { get; set; } = "connectedClients";

        public string BasePath { get; set; } = DefaultBasePath;
        public string EndPointPrefix { get; set; } = "client";
        public TimeSpan GarbageCollectionDelayMax { get; set; } = DefaultGarbageCollectionDelayMax;
    }
}
