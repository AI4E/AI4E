using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging.InMemory.Sample
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            // First we need to get an instance of our di container
            var serviceProvider = BuildServiceProvider();

            // Resolve the message dispatcher
            var messageDispatcher = serviceProvider.GetRequiredService<IMessageDispatcher>();

            // Dispatch a log command
            var command = new LogCommand(new[] { "Hello", "world", "!" }.ToImmutableArray());
            var commandResult = await messageDispatcher.DispatchAsync(command);

            Console.WriteLine(commandResult);
            Console.WriteLine(commandResult.GetType());
            Console.ReadLine();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Add the in memory messaging system
            services.AddInMemoryMessaging();

            // Add a service to demonstrate di
            services.AddSingleton<IConsoleLogger, ConsoleLogger>();
        }

        private static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            return services.BuildServiceProvider();
        }
    }

    public interface IConsoleLogger
    {
        Task LogToConsoleAsync(string str);
    }

    public sealed class ConsoleLogger : IConsoleLogger
    {
        public Task LogToConsoleAsync(string str)
        {
            return Console.Out.WriteLineAsync("Logger: " + str);
        }
    }

    public sealed class LoggedEvent
    {
        public LoggedEvent(string str)
        {
            LoggedString = str;
        }

        public string LoggedString { get; }
    }

    public sealed class LogCommand
    {
        public LogCommand(ImmutableArray<string> components)
        {
            Components = components;
        }

        public ImmutableArray<string> Components { get; }
    }

    public sealed class LoggerCommandHandler : MessageHandler
    {
        private readonly IConsoleLogger _logger;

        // Our service is injected into the constructor
        public LoggerCommandHandler(IConsoleLogger logger)
        {
            _logger = logger;
        }

        public async Task<IDispatchResult> HandleAsync(LogCommand message)
        {
            var str = message.Components.Aggregate((e, n) => e + " " + n);

            // We use the service that is injected into out constructor.
            await _logger.LogToConsoleAsync(str);

            var evt = new LoggedEvent(str);

            // We use the MessageDispatcher that is injected into our handler to publish an event.
            var dispatchResult = await MessageDispatcher.DispatchAsync(evt, publish: true);

            // We return the dispatch result of the dispatch to forward any failures to the caller.
            return dispatchResult;
        }

        // A message handle is able to handle multiple messages by defining mutliple handle methods.
        public void Handle(LoggedEvent message)
        {
            Console.WriteLine("Handled event: " + message.LoggedString);
        }
    }

    public sealed class LoggerEventHandler : MessageHandler
    {
        // This action method handles messages of arbitrary types.
        public IDispatchResult Handle(object message)
        {
            Console.WriteLine("Handled event of type: " + message.GetType());

            // We have declared the method to return a dispatch result explicitly. The base type declared convenience methods for this.

            // This is not a real use case but to show the principle.
            if (message is LogCommand)
            {
                return Failure("The message type must not me of type 'LogCommand'.");
            }

            return Success();
        }
    }
}
