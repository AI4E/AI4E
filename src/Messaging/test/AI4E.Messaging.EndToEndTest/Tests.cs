using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Messaging.Validation;
using AI4E.Utils.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.EndToEndTest
{
    [TestClass]
    public class Tests
    {
        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddMessaging()
                .UseValidation();

            // This is needed because the entry assembly is the testhost.
            services.ConfigureApplicationParts(partManager =>
                partManager.ApplicationParts.Add(
                    new AssemblyPart(Assembly.GetExecutingAssembly())));

            services.AddSingleton<TestService>();
        }

        private ServiceProvider ServiceProvider { get; set; }
        private IMessageDispatcher MessageDispatcher { get; set; }

        [TestInitialize]
        public void Setup()
        {
            ServiceProvider = ConfigureServices();
            MessageDispatcher = ServiceProvider.GetRequiredService<IMessageDispatcher>();
        }

        [TestCleanup]
        public void TearDown()
        {
            ServiceProvider.Dispose();
        }

        [TestMethod]
        public async Task DispatchTest()
        {
            var message = new TestMessage(5, "abc");
            var result = await MessageDispatcher.DispatchAsync(message);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(("abc", 1), (result as SuccessDispatchResult<(string str, int one)>)?.Result);
        }

        [TestMethod]
        public async Task PublishTest()
        {
            var message = new TestEvent(5);
            var result = await MessageDispatcher.DispatchAsync(message, publish: true);
            var success = result.IsSuccessWithResults<int>(out var results);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(success);
            Assert.AreEqual(2, results.Count());
            Assert.IsTrue(results.All(p => p == 5));
        }

        [TestMethod]
        public async Task MultipleResultsTest()
        {
            var message = new OtherMessage(5);
            var result = await MessageDispatcher.DispatchAsync(message);
            var success = result.IsSuccessWithResults<double>(out var results);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(success);
            Assert.AreEqual(2, results.Count());
            Assert.AreEqual(5, results.First());
            Assert.AreEqual(6, results.Last());
        }

        [TestMethod]
        public async Task ValidateTest()
        {
            var message = new OtherMessage(-1);
            var result = await MessageDispatcher.DispatchAsync(message);
            var isValidationFailed = result.IsValidationFailed(out var validationResults);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(isValidationFailed);
            Assert.AreEqual(1, validationResults.Count());
            Assert.AreEqual(nameof(OtherMessage.Amount), validationResults.First().Member);
            Assert.AreEqual("Value must be greater or equal zero.", validationResults.First().Message);
        }

        [TestMethod]
        public async Task ValidationDispatchFailureTest()
        {
            var message = Validate.Create(new OtherMessage(-1));
            var result = await MessageDispatcher.DispatchAsync(message);
            var isValidationFailed = result.IsValidationFailed(out var validationResults);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(isValidationFailed);
            Assert.AreEqual(1, validationResults.Count());
            Assert.AreEqual(nameof(OtherMessage.Amount), validationResults.First().Member);
            Assert.AreEqual("Value must be greater or equal zero.", validationResults.First().Message);
        }

        [TestMethod]
        public async Task ValidationDispatchSuccessTest()
        {
            var message = Validate.Create(new OtherMessage(5));
            var result = await MessageDispatcher.DispatchAsync(message);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.GetType() == typeof(SuccessDispatchResult));
        }
    }

    public sealed class TestEvent
    {
        public TestEvent(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class TestMessage : TestMessageBase
    {
        public TestMessage(int @int, string str) : base(str)
        {
            Int = @int;
        }

        public int Int { get; }
    }

    public abstract class TestMessageBase
    {
        protected TestMessageBase(string str)
        {
            Str = str;
        }

        public string Str { get; }
    }

    public sealed class OtherMessage
    {
        public OtherMessage(double amount)
        {
            Amount = amount;
        }

        public double Amount { get; }
    }

    public sealed class TestService
    {
        public int GetOne()
        {
            return 1;
        }
    }

    public sealed class TestMessageHandler : MessageHandler
    {
        public int Handle(TestEvent @event)
        {
            return @event.Value;
        }

        public (string str, int one) Handle(TestMessageBase message, TestService testService)
        {
            return (message.Str, testService.GetOne());
        }

        [Validate]
        public async IAsyncEnumerable<double> HandleAsync(OtherMessage message)
        {
            await Task.Yield();
            yield return message.Amount;
            yield return message.Amount + 1;
        }

        public void Validate(OtherMessage message, ValidationResultsBuilder validationResults)
        {
            if (message.Amount < 0)
            {
                validationResults.AddValidationResult(nameof(message.Amount), "Value must be greater or equal zero.");
            }
        }
    }

    public sealed class TestEventHandler
    {
        public int Handle(TestEvent @event)
        {
            return @event.Value;
        }
    }
}
