using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Test;
using AI4E.Messaging.Validation;
using AI4E.Utils;
using AI4E.Utils.ApplicationParts;
using AI4E.Utils.Async;
using AI4E.Utils.DependencyInjection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.EndToEndTest
{
    [TestClass]
    public sealed class CustomLoadContextTests
    {
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            services.AddAutofacChildContainerBuilder();
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);
            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddMessaging()
                .UseValidation();

            services.AddSingleton<TestService>();
        }

        private static void ConfigureChildServices(
            IServiceCollection services,
            Assembly assembly,
            bool configureHandlers)
        {
            services.AddMessaging(suppressRoutingSystem: true)
                    .UseValidation()
                    .UseTypeResolver(new TypeResolver(assembly.Yield(), fallbackToDefaultContext: true));

            services.Configure<MessagingOptions>(
                      options => options.LocalEndPoint = new RouteEndPointAddress(Guid.NewGuid().ToString()));

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

        private IServiceProvider ServiceProvider { get; set; }

        // Context 1
        private TestAssemblyLoadContext AssemblyLoadContext1 { get; set; }
        private Assembly Assembly1 { get; set; }
        private IMessageDispatcher MessageDispatcher1 { get; set; }

        // Context 2
        private TestAssemblyLoadContext AssemblyLoadContext2 { get; set; }
        private Assembly Assembly2 { get; set; }
        private IMessageDispatcher MessageDispatcher2 { get; set; }

        [TestInitialize]
        public async Task SetupAsync()
        {
            ServiceProvider = ConfigureServices();
            var childContainerBuilder = ServiceProvider.GetRequiredService<IChildContainerBuilder>();

            AssemblyLoadContext1 = new TestAssemblyLoadContext();
            Assembly1 = AssemblyLoadContext1.TestAssembly;
            AssemblyLoadContext2 = new TestAssemblyLoadContext();
            Assembly2 = AssemblyLoadContext2.TestAssembly;

            var servicesDescriptor1 = childContainerBuilder.CreateChildContainer(
                services => ConfigureChildServices(services, Assembly1, configureHandlers: false));

            var servicesDescriptor2 = childContainerBuilder.CreateChildContainer(
                services => ConfigureChildServices(services, Assembly2, configureHandlers: false));

            MessageDispatcher1 = servicesDescriptor1.GetRequiredService<IMessageDispatcher>();
            MessageDispatcher2 = servicesDescriptor2.GetRequiredService<IMessageDispatcher>();

            await (MessageDispatcher1 as IAsyncInitialization).Initialization;
            await (MessageDispatcher2 as IAsyncInitialization).Initialization;
        }

        [TestCleanup]
        public void TearDown()
        {
            ServiceProvider.DisposeIfDisposable();
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
            Assert.AreEqual("abc", (string)((dynamic)queryResult).Str);
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
