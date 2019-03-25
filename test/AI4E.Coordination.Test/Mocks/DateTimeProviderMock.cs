using System;

namespace AI4E.Coordination.Mocks
{
    public sealed class DateTimeProviderMock : IDateTimeProvider
    {
        public DateTimeProviderMock(DateTime currentTime)
        {
            CurrentTime = currentTime;
        }

        public DateTimeProviderMock()
        {
            CurrentTime = DateTime.UtcNow;
        }

        DateTime IDateTimeProvider.GetCurrentTime()
        {
            return CurrentTime;
        }

        public DateTime CurrentTime { get; set; }
    }
}
