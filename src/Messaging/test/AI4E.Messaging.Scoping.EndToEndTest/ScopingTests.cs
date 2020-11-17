/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Scoping.EndToEndTest.Messages;
using AI4E.Messaging.Scoping.EndToEndTest.Services;
using AI4E.Utils.DependencyInjection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// TODO: Dispatch to another message dispatcher with the same end-point
// We have to reconsider this when we enable clustering, that is when it is possible to have multiple 
// route end-points for a single route end-point address in a routing system.

namespace AI4E.Messaging.Scoping.EndToEndTest
{
    public class ScopingTests
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
            services.AddMessaging();

            var setup = new Setup();
            setup.ConfigureServices(services);
        }

        private static void ConfigureClientServices(
            IServiceCollection services,
            string endPoint,
            bool configureHandlers)
        {
            services.AddMessaging(suppressRoutingSystem: true).Configure(options =>
            {
                options.LocalEndPoint = new RouteEndPointAddress(endPoint);
            });

            services.ConfigureAssemblyRegistry(registry =>
            {
                registry.ClearAssemblies();

                if (configureHandlers)
                {
                    registry.AddAssembly(Assembly.GetExecutingAssembly());
                }
            });
        }

        public ScopingTests()
        {
            var parentServiceProvider = ConfigureServices();
            var childContainerBuilder = parentServiceProvider.GetRequiredService<IChildContainerBuilder>();

            ServiceProvider1 = childContainerBuilder.CreateChildContainer(
                services => ConfigureClientServices(services, "EndPoint1", configureHandlers: true));

            ServiceProvider2 = childContainerBuilder.CreateChildContainer(
              services => ConfigureClientServices(services, "EndPoint1", configureHandlers: true));

            ServiceProvider3 = childContainerBuilder.CreateChildContainer(
              services => ConfigureClientServices(services, "EndPoint2", configureHandlers: true));
        }

        public GlobalAssertionLookup GlobalAssertionLookup => ServiceProvider1.GetRequiredService<GlobalAssertionLookup>();

        public IServiceProvider ServiceProvider1 { get; }
        public IMessageDispatcher MessageDispatcher1 => ServiceProvider1.GetRequiredService<IMessageDispatcher>();
       
        public IServiceProvider ServiceProvider2 { get; }
        public IMessageDispatcher MessageDispatcher2 => ServiceProvider2.GetRequiredService<IMessageDispatcher>();

        public IServiceProvider ServiceProvider3 { get; }
        public IMessageDispatcher MessageDispatcher3 => ServiceProvider3.GetRequiredService<IMessageDispatcher>();

        [Fact]
        public async Task DispatchToRegisteredHandlersTest()
        {
            // Arrange

            // Create a scoped service container and get the scoped message dispatcher from it
            using var serviceScope = ServiceProvider1.CreateScope();
            var scopedServiceProvider = serviceScope.ServiceProvider;
            var scopedMessageDispatcher = scopedServiceProvider.GetRequiredService<IMessageDispatcher>();

            var registerCallbackCommand = new RegisterCallbackCommand();

            var triggerCallbackCommand = new TriggerCallbackCommand();

            var scopedService = scopedServiceProvider.GetRequiredService<ScopedService>();

            // Act

            // Use the scoped message dispatcher to dispatch the register message
            var registerResult = await scopedMessageDispatcher.DispatchAsync(registerCallbackCommand);

            // Use the global dispatcher to dispatch a trigger callback message
            var triggerResult = await MessageDispatcher1.DispatchAsync(triggerCallbackCommand);

            // Assert

            Assert.True(registerResult.IsSuccess);
            Assert.True(triggerResult.IsSuccess);

            // The scoped service should now be included in the global assertion lookup
            Assert.Single(GlobalAssertionLookup.HandledServices, expected: scopedService);
        }

        // Dispatch to another message dispatcher with the same end-point
        [Fact]
        public async Task DispatchToRegisteredHandlersOnOtherDispatcherWithSameEndPointTest()
        {
            // Arrange

            // Create a scoped service container and get the scoped message dispatcher from it
            using var serviceScope = ServiceProvider1.CreateScope();
            var scopedServiceProvider = serviceScope.ServiceProvider;
            var scopedMessageDispatcher = scopedServiceProvider.GetRequiredService<IMessageDispatcher>();

            var registerCallbackCommand = new RegisterCallbackCommand();

            var triggerCallbackCommand = new TriggerCallbackCommand();

            var scopedService = scopedServiceProvider.GetRequiredService<ScopedService>();

            // Act

            // Use the scoped message dispatcher to dispatch the register message
            var registerResult = await scopedMessageDispatcher.DispatchAsync(registerCallbackCommand);

            // Use ANOTHER global dispatcher with the SAME end-point address to dispatch a trigger callback message
            var triggerResult = await MessageDispatcher2.DispatchAsync(triggerCallbackCommand);

            // Assert

            Assert.True(registerResult.IsSuccess);
            Assert.True(triggerResult.IsSuccess);

            // The scoped service should now be included in the global assertion lookup
            Assert.Single(GlobalAssertionLookup.HandledServices, expected: scopedService);
        }

        // Dispatch to another message dispatcher with another end-point
        [Fact]
        public async Task DispatchToRegisteredHandlersOnOtherDispatcherWithOtherEndPointTest()
        {
            // Arrange

            // Create a scoped service container and get the scoped message dispatcher from it
            using var serviceScope = ServiceProvider1.CreateScope();
            var scopedServiceProvider = serviceScope.ServiceProvider;
            var scopedMessageDispatcher = scopedServiceProvider.GetRequiredService<IMessageDispatcher>();

            var registerCallbackCommand = new RegisterCallbackCommand();

            var triggerCallbackCommand = new TriggerCallbackCommand();

            var scopedService = scopedServiceProvider.GetRequiredService<ScopedService>();

            // Act

            // Use the scoped message dispatcher to dispatch the register message
            var registerResult = await scopedMessageDispatcher.DispatchAsync(registerCallbackCommand);

            // Use ANOTHER global dispatcher with ANOTHER end-point address to dispatch a trigger callback message
            var triggerResult = await MessageDispatcher3.DispatchAsync(triggerCallbackCommand);

            // Assert

            Assert.True(registerResult.IsSuccess);
            Assert.True(triggerResult.IsSuccess);

            // The scoped service should now be included in the global assertion lookup
            Assert.Single(GlobalAssertionLookup.HandledServices, expected: scopedService);
        }
    }
}
