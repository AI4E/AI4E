using System;

namespace AI4E.Routing.Blazor.Sample.App.Services
{
    public sealed class WeatherForecastQuery
    {
        public WeatherForecastQuery(DateTime startDate)
        {
            StartDate = startDate;
        }

        public DateTime StartDate { get; }
    }
}
