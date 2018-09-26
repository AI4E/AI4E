using System;

namespace AI4E.Coordination
{
    public class CoordinationManagerOptions
    {
        internal static TimeSpan LeaseLengthDefault { get; } = TimeSpan.FromSeconds(30);

        public TimeSpan LeaseLength { get; set; } = LeaseLengthDefault;
    }
}
