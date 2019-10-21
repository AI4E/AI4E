using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AI4E.Messaging.Validation;
using AI4E.Utils.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.EndToEndTest
{
    [TestClass]
    public sealed class CustomLoadContextTests
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
        private AssemblyLoadContext AssemblyLoadContext { get; set; }
        private Assembly MessageAssembly { get; set; }

        [TestInitialize]
        public void Setup()
        {
            ServiceProvider = ConfigureServices();
            MessageDispatcher = ServiceProvider.GetRequiredService<IMessageDispatcher>();
            AssemblyLoadContext = new TestAssemblyLoadContext(Assembly.GetExecutingAssembly().Location);
            MessageAssembly = AssemblyLoadContext.LoadFromAssemblyName(Assembly.GetExecutingAssembly().GetName());
        }

        [TestCleanup]
        public void TearDown()
        {
            ServiceProvider.Dispose();
        }

        [TestMethod]
        public async Task DispatchTest()
        {
            var messageType = MessageAssembly.GetType(typeof(TestMessage).FullName);
            var message = Activator.CreateInstance(messageType, 5, "abc");
            var result = await MessageDispatcher.DispatchAsync(DispatchDataDictionary.Create(messageType, message));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(("abc", 1), (result as SuccessDispatchResult<(string str, int one)>)?.Result);
        }

        [TestMethod]
        public async Task PublishTest()
        {
            var messageType = MessageAssembly.GetType(typeof(TestEvent).FullName);
            var message = Activator.CreateInstance(messageType, 5);
            var result = await MessageDispatcher.DispatchAsync(DispatchDataDictionary.Create(messageType, message), publish: true);
            var success = result.IsSuccessWithResults<int>(out var results);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(success);
            Assert.AreEqual(2, results.Count());
            Assert.IsTrue(results.All(p => p == 5));
        }

        [TestMethod]
        public async Task MultipleResultsTest()
        {
            var messageType = MessageAssembly.GetType(typeof(OtherMessage).FullName);
            var message = Activator.CreateInstance(messageType, 5);
            var result = await MessageDispatcher.DispatchAsync(DispatchDataDictionary.Create(messageType, message));
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
            var messageType = MessageAssembly.GetType(typeof(OtherMessage).FullName);
            var message = Activator.CreateInstance(messageType, -1);
            var result = await MessageDispatcher.DispatchAsync(DispatchDataDictionary.Create(messageType, message));
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
            var underlyingMessageType = MessageAssembly.GetType(typeof(OtherMessage).FullName);
            var underlyingMessage = Activator.CreateInstance(underlyingMessageType, -1);
            var messageType = typeof(Validate<>).MakeGenericType(underlyingMessageType);
            var message = Activator.CreateInstance(messageType, underlyingMessage);
            var result = await MessageDispatcher.DispatchAsync(DispatchDataDictionary.Create(messageType, message));
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
            var underlyingMessageType = MessageAssembly.GetType(typeof(OtherMessage).FullName);
            var underlyingMessage = Activator.CreateInstance(underlyingMessageType, 5);
            var messageType = typeof(Validate<>).MakeGenericType(underlyingMessageType);
            var message = Activator.CreateInstance(messageType, underlyingMessage);
            var result = await MessageDispatcher.DispatchAsync(DispatchDataDictionary.Create(messageType, message));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.GetType() == typeof(SuccessDispatchResult));
        }

        [TestMethod]
        public async Task ResultTest()
        {
            var message = new CustomQuery();
            var result = await MessageDispatcher.DispatchAsync(DispatchDataDictionary.Create(message.GetType(), message));
            var success = result.IsSuccess(out var queryResult);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(success);
            Assert.IsNotNull(queryResult);
            Assert.IsInstanceOfType(queryResult, typeof(CustomQueryResult));
        }
    }

    internal sealed class TestAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public TestAssemblyLoadContext(string mainAssemblyToLoadPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(mainAssemblyToLoadPath);
        }

        protected override Assembly Load(AssemblyName name)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(name);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }

    public sealed class CustomQueryHandler
    {
        public IDispatchResult Handle(CustomQuery query)
        {
            var assemblyLoadContext = new TestAssemblyLoadContext(Assembly.GetExecutingAssembly().Location);
            var messageAssembly = assemblyLoadContext.LoadFromAssemblyName(Assembly.GetExecutingAssembly().GetName());

            var resultType = messageAssembly.GetType(typeof(CustomQueryResult).FullName);
            var result = Activator.CreateInstance(resultType, "abc");
            return SuccessDispatchResult.FromResult(resultType, result);
        }
    }

    public sealed class CustomQuery { }

    public sealed class CustomQueryResult
    {
        public CustomQueryResult(string str)
        {
            Str = str;
        }

        public string Str { get; set; }
    }
}
