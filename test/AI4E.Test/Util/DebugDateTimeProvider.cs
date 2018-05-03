using System;

namespace AI4E.Test.Util
{
    public sealed class DebugDateTimeProvider : IDateTimeProvider
    {
        public DateTime CurrentTime { get; set; } = DateTime.Now;

        DateTime IDateTimeProvider.GetCurrentTime()
        {
            return CurrentTime;
        }
    }
}
