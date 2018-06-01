using AI4E;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicMessaging
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = serviceProvider.GetRequiredService<IMessageDispatcher>();
            var result = await messageDispatcher.DispatchAsync(new PrintCommand(new[] { "Hello", "world", "!" }.ToImmutableArray()));
            Console.WriteLine(result);
            Console.ReadLine();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddInMemoryMessaging(options =>
            {

            });
            services.AddSingleton<ILogToConsole, Logging>();
        }

        private static IServiceProvider BuildServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            return serviceCollection.BuildServiceProvider();
        }
    }

    public class LoggingMessageHandler : MessageHandler
    {
        ILogToConsole _logger;

        public LoggingMessageHandler(ILogToConsole logger)
        {
            _logger = logger;
        }

        public async Task HandleAsync(PrintCommand message)
        {
            Console.WriteLine("Message received.");
            await _logger.LogAsync(message.Component.Aggregate(seed: new StringBuilder(), (e,n)=> e.Append(n).Append(' ')).ToString());
        }

        [Action(typeof(PrintCommand))]
        public async Task<IDispatchResult> HandleAsync(Object message)
        {
            Console.WriteLine("Event received.");      
            return Success();
        }
    }
}
