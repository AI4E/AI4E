using System;

namespace AI4E.Coordination
{
    public class CoordinationManagerOptions
    {
        internal static TimeSpan LeaseLengthDefault { get; } = TimeSpan.FromSeconds(30);
        internal static string MultiplexPrefixDefault { get; } = "coord/";

        public TimeSpan LeaseLength { get; set; } = LeaseLengthDefault;
        public string MultiplexPrefix { get; set; } = MultiplexPrefixDefault;
    }
}
