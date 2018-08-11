using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Routing.Blazor.Sample.App.Services;

namespace AI4E.Routing.Blazor.Sample.Server.Services
{
    public sealed class WeatherForecastQueryHandler : MessageHandler
    {
        private static string[] _summaries = new[]
          {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public Task<WeatherForecast[]> HandleAsync(WeatherForecastQuery query)
        {
            var startDate = query.StartDate;

            var rng = new Random();
            return Task.FromResult(Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = startDate.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = _summaries[rng.Next(_summaries.Length)]
            }).ToArray());
        }
    }
}
