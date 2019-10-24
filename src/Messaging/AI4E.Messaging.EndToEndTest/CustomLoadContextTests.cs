using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AI4E.Messaging.Validation;
using AI4E.Utils;
using AI4E.Utils.ApplicationParts;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.EndToEndTest
{
    [TestClass]
    public sealed class CustomLoadContextTests
    {
        private static ContextServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            return services.BuildContextServiceProvider(validateScopes: true);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddMessaging()
                .UseValidation();

            services.AddSingleton<TestService>();
        }

        private static void ConfigureContextServices(
            IServiceCollection services,
            Assembly assembly,
            bool configureHandlers)
        {
            services.AddMessaging()
                    .UseValidation()
                    .UseTypeResolver(new TypeResolver(assembly.Yield(), fallbackToDefaultContext: true));

            services.AddSingleton(assembly.GetType(typeof(TestService).FullName));

            if (configureHandlers)
            {
                services.ConfigureApplicationParts(partManager =>
                {
                    partManager.ApplicationParts.Clear();
                    partManager.ApplicationParts.Add(new AssemblyPart(assembly));
                });
            }
        }

        private ContextServiceProvider ServiceProvider { get; set; }

        // Context 1
        private AssemblyLoadContext AssemblyLoadContext1 { get; set; }
        private Assembly Assembly1 { get; set; }
        private IMessageDispatcher MessageDispatcher1 { get; set; }

        // Context 2
        private AssemblyLoadContext AssemblyLoadContext2 { get; set; }
        private Assembly Assembly2 { get; set; }
        private IMessageDispatcher MessageDispatcher2 { get; set; }

        [TestInitialize]
        public async Task SetupAsync()
        {
            ServiceProvider = ConfigureServices();
            var contextServiceManager = ServiceProvider.GetRequiredService<IContextServiceManager>();

            AssemblyLoadContext1 = new TestAssemblyLoadContext(Assembly.GetExecutingAssembly().Location);
            Assembly1 = AssemblyLoadContext1.LoadFromAssemblyName(Assembly.GetExecutingAssembly().GetName());
            AssemblyLoadContext2 = new TestAssemblyLoadContext(Assembly.GetExecutingAssembly().Location);
            Assembly2 = AssemblyLoadContext2.LoadFromAssemblyName(Assembly.GetExecutingAssembly().GetName());

            contextServiceManager.TryConfigureContextServices(
                "context1",
                services => ConfigureContextServices(services, Assembly1, configureHandlers: false),
                out var servicesDescriptor1);

            contextServiceManager.TryConfigureContextServices(
                "context2",
                services => ConfigureContextServices(services, Assembly2, configureHandlers: true),
                out var servicesDescriptor2);

            MessageDispatcher1 = servicesDescriptor1.GetRequiredService<IMessageDispatcher>();
            MessageDispatcher2 = servicesDescriptor2.GetRequiredService<IMessageDispatcher>();

            await (MessageDispatcher1 as IAsyncInitialization).Initialization;
            await (MessageDispatcher2 as IAsyncInitialization).Initialization;
        }

        [TestCleanup]
        public void TearDown()
        {
            ServiceProvider.Dispose();
        }

        [TestMethod]
        public async Task DispatchTest()
        {
            var messageType = Assembly1.GetType(typeof(TestMessage).FullName);
            var message = Activator.CreateInstance(messageType, 5, "abc");
            var result = await MessageDispatcher1.DispatchAsync(DispatchDataDictionary.Create(messageType, message));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(("abc", 1), (result as SuccessDispatchResult<(string str, int one)>)?.Result);
        }

        [TestMethod]
        public async Task PublishTest()
        {
            var messageType = Assembly1.GetType(typeof(TestEvent).FullName);
            var message = Activator.CreateInstance(messageType, 5);
            var result = await MessageDispatcher1.DispatchAsync(DispatchDataDictionary.Create(messageType, message), publish: true);
            var success = result.IsSuccessWithResults<int>(out var results);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(success);
            Assert.AreEqual(2, results.Count());
            Assert.IsTrue(results.All(p => p == 5));
        }

        [TestMethod]
        public async Task MultipleResultsTest()
        {
            var messageType = Assembly1.GetType(typeof(OtherMessage).FullName);
            var message = Activator.CreateInstance(messageType, 5);
            var result = await MessageDispatcher1.DispatchAsync(DispatchDataDictionary.Create(messageType, message));
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
            var messageType = Assembly1.GetType(typeof(OtherMessage).FullName);
            var message = Activator.CreateInstance(messageType, -1);
            var result = await MessageDispatcher1.DispatchAsync(DispatchDataDictionary.Create(messageType, message));
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
            var underlyingMessageType = Assembly1.GetType(typeof(OtherMessage).FullName);
            var underlyingMessage = Activator.CreateInstance(underlyingMessageType, -1);
            var messageType = typeof(Validate<>).MakeGenericType(underlyingMessageType);
            var message = Activator.CreateInstance(messageType, underlyingMessage);
            var result = await MessageDispatcher1.DispatchAsync(DispatchDataDictionary.Create(messageType, message));
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
            var underlyingMessageType = Assembly1.GetType(typeof(OtherMessage).FullName);
            var underlyingMessage = Activator.CreateInstance(underlyingMessageType, 5);
            var messageType = typeof(Validate<>).MakeGenericType(underlyingMessageType);
            var message = Activator.CreateInstance(messageType, underlyingMessage);
            var result = await MessageDispatcher1.DispatchAsync(DispatchDataDictionary.Create(messageType, message));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.GetType() == typeof(SuccessDispatchResult));
        }

        [TestMethod]
        public async Task ResultTest()
        {
            var resultType = Assembly1.GetType(typeof(CustomQueryResult).FullName);
            var messageType = Assembly1.GetType(typeof(CustomQuery).FullName);
            var message = Activator.CreateInstance(messageType);
            var result = await MessageDispatcher1.DispatchAsync(DispatchDataDictionary.Create(messageType, message));
            var success = result.IsSuccess(out var queryResult);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(success);
            Assert.IsNotNull(queryResult);
            Assert.IsInstanceOfType(queryResult, resultType);
            Assert.AreEqual("abc", ((dynamic)queryResult).Str);
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
            if (assemblyPath != null && name.Name.Equals(Assembly.GetExecutingAssembly().GetName().Name, StringComparison.Ordinal))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }

    public sealed class CustomQueryHandler
    {
        public CustomQueryResult Handle(CustomQuery query)
        {
            return new CustomQueryResult("abc");
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
