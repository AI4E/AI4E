﻿using System;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Remoting;
using AI4E.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Coordination
{
    [TestClass]
    public class CoordinationManagerTest
    {
        [TestMethod]
        public async Task LoadNonExistingEntryTest()
        {
            var (coordinationManagerX, coordinationManagerY, _) = BuildCoordinationSystem();

            var entryX = await coordinationManagerX.GetAsync("/");
            var entryY = await coordinationManagerY.GetAsync("/");

            Assert.IsNull(entryX);
            Assert.IsNull(entryY);
        }

        [TestMethod]
        public async Task CreateEntryTest()
        {
            byte[] payload = { 1, 2, 3 };

            var (coordinationManagerX, coordinationManagerY, _) = BuildCoordinationSystem();

            await coordinationManagerX.CreateAsync("/", payload, EntryCreationModes.Default);

            var entryX = await coordinationManagerX.GetAsync("/");
            var entryY = await coordinationManagerY.GetAsync("/");

            Assert.IsNotNull(entryX);
            Assert.AreEqual(1, entryX.Value[0]);
            Assert.AreEqual(2, entryX.Value[1]);
            Assert.AreEqual(3, entryX.Value[2]);

            Assert.AreEqual("/", entryX.Path);
            Assert.AreEqual(1, entryX.Version);
            Assert.IsNotNull(entryX.Children);
            Assert.AreEqual(0, entryX.Children.Count);

            Assert.IsNotNull(entryY);
            Assert.AreEqual(1, entryY.Value[0]);
            Assert.AreEqual(2, entryY.Value[1]);
            Assert.AreEqual(3, entryY.Value[2]);

            Assert.AreEqual("/", entryY.Path);
            Assert.AreEqual(1, entryY.Version);
            Assert.IsNotNull(entryY.Children);
            Assert.AreEqual(0, entryY.Children.Count);
        }

        [TestMethod]
        public async Task UpdateEntryTest()
        {
            byte[] payload = { 1, 2, 3 };
            byte[] newPayload = { 4, 5, 6 };

            var (coordinationManagerX, coordinationManagerY, _) = BuildCoordinationSystem();

            await coordinationManagerX.CreateAsync("/", payload, EntryCreationModes.Default);

            // Load entries in order to fill the caches
            var entryX_ = await coordinationManagerX.GetAsync("/");
            var entryY_ = await coordinationManagerY.GetAsync("/");

            var result = await coordinationManagerY.SetValueAsync("/", newPayload, version: 1);

            Assert.AreEqual(1, result);

            var entryX = await coordinationManagerX.GetAsync("/");
            var entryY = await coordinationManagerY.GetAsync("/");

            Assert.IsNotNull(entryX);
            Assert.AreEqual(4, entryX.Value[0]);
            Assert.AreEqual(5, entryX.Value[1]);
            Assert.AreEqual(6, entryX.Value[2]);

            Assert.AreEqual("/", entryX.Path);
            Assert.AreEqual(2, entryX.Version);
            Assert.IsNotNull(entryX.Children);
            Assert.AreEqual(0, entryX.Children.Count);

            Assert.IsNotNull(entryY);
            Assert.AreEqual(4, entryY.Value[0]);
            Assert.AreEqual(5, entryY.Value[1]);
            Assert.AreEqual(6, entryY.Value[2]);

            Assert.AreEqual("/", entryY.Path);
            Assert.AreEqual(2, entryY.Version);
            Assert.IsNotNull(entryY.Children);
            Assert.AreEqual(0, entryY.Children.Count);
        }

        [TestMethod]
        public async Task EphemeralNodeTest()
        {
            byte[] payload = { 1, 2, 3 };

            var (coordinationManagerX, coordinationManagerY, sessionManagerX) = BuildCoordinationSystem();

            await coordinationManagerX.CreateAsync("/", payload, EntryCreationModes.Ephemeral);

            var entryX = await coordinationManagerX.GetAsync("/");
            var entryY = await coordinationManagerY.GetAsync("/");

            Assert.IsNotNull(entryX);
            Assert.AreEqual(1, entryX.Value[0]);
            Assert.AreEqual(2, entryX.Value[1]);
            Assert.AreEqual(3, entryX.Value[2]);

            Assert.AreEqual("/", entryX.Path);
            Assert.AreEqual(1, entryX.Version);
            Assert.IsNotNull(entryX.Children);
            Assert.AreEqual(0, entryX.Children.Count);

            Assert.IsNotNull(entryY);
            Assert.AreEqual(1, entryY.Value[0]);
            Assert.AreEqual(2, entryY.Value[1]);
            Assert.AreEqual(3, entryY.Value[2]);

            Assert.AreEqual("/", entryY.Path);
            Assert.AreEqual(1, entryY.Version);
            Assert.IsNotNull(entryY.Children);
            Assert.AreEqual(0, entryY.Children.Count);

            (coordinationManagerX as IDisposable).Dispose();

            await sessionManagerX.WaitForTerminationAsync(await coordinationManagerX.GetSessionAsync());

            // Wait some more time to give the garbage collector the time to remove any ephemeral nodes

            await Task.Delay(2000);

            entryY = await coordinationManagerY.GetAsync("/");

            Assert.IsNull(entryY);
        }

        private IServiceProvider BuildServiceProvider<TStorage>(IPhysicalEndPoint<TestAddress> physicalEndPoint,
                                                                IDateTimeProvider dateTimeProvider,
                                                                IStoredEntryManager storedEntryManager,
                                                                IStoredSessionManager storedSessionManager,
                                                                TStorage coordinationStorage)
            where TStorage : ICoordinationStorage, ISessionStorage
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IAddressConversion<TestAddress>, TestAddressSerializer>();
            services.AddSingleton(physicalEndPoint);
            services.AddSingleton<ICoordinationStorage>(coordinationStorage);
            services.AddSingleton<ISessionStorage>(coordinationStorage);
            services.AddSingleton<ISessionManager, SessionManager>();
            services.AddSingleton<ICoordinationManager, CoordinationManager>();
            services.AddSingleton(p => Provider.FromServices<ICoordinationManager>(p));
            services.AddSingleton<ICoordinationCallback, CoordinationCallback<TestAddress>>();
            services.AddSingleton(p => p.GetRequiredService<ICoordinationCallback>() as ISessionProvider);
            services.AddSingleton(dateTimeProvider);
            services.AddSingleton(storedEntryManager);
            services.AddSingleton(storedSessionManager);
            services.AddLogging(options =>
            {
                options.SetMinimumLevel(LogLevel.Trace);
                options.AddDebug();

            });
            return services.BuildServiceProvider();
        }

        private (ICoordinationManager x, ICoordinationManager y, ISessionManager sessionManagerX) BuildCoordinationSystem()
        {
            var dateTimeProvider = new DateTimeProvider();
            var storedSessionManager = new StoredSessionManager(dateTimeProvider);
            var storedEntryManager = new StoredEntryManager(dateTimeProvider);

            var messagingSubsystem = new PhysicalMessingSubsystem();
            var coordinationStorage = new InMemoryCoordinationStorage(dateTimeProvider, storedEntryManager, storedSessionManager);
            var serviceProviderX = BuildServiceProvider(messagingSubsystem.X, dateTimeProvider, storedEntryManager, storedSessionManager, coordinationStorage);
            var serviceProviderY = BuildServiceProvider(messagingSubsystem.Y, dateTimeProvider, storedEntryManager, storedSessionManager, coordinationStorage);

            return (serviceProviderX.GetRequiredService<ICoordinationManager>(),
                    serviceProviderY.GetRequiredService<ICoordinationManager>(),
                    serviceProviderX.GetRequiredService<ISessionManager>());
        }
    }
}
