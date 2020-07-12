/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Threading.Tasks;
using AI4E.Storage.Coordination.Mocks;
using AI4E.Storage.Coordination.Session;
using AI4E.Remoting;
using AI4E.Remoting.Utils;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace AI4E.Storage.Coordination.Locking
{
    [TestClass]
    public class CoordinationExchangeManagerTests
    {
        public TestMessagingSystem MessagingSystem { get; set; }
        public IPhysicalEndPoint<TestMessagingSystemAddress> PhysicalEndPoint1 { get; set; }
        public IPhysicalEndPoint<TestMessagingSystemAddress> PhysicalEndPoint2 { get; set; }
        public IPhysicalEndPoint<TestMessagingSystemAddress> PhysicalEndPoint3 { get; set; }
        public PhysicalEndPointMultiplexer<TestMessagingSystemAddress> PhysicalEndPointMultiplexer1 { get; set; }
        public PhysicalEndPointMultiplexer<TestMessagingSystemAddress> PhysicalEndPointMultiplexer2 { get; set; }
        public PhysicalEndPointMultiplexer<TestMessagingSystemAddress> PhysicalEndPointMultiplexer3 { get; set; }

        public DateTimeProviderMock DateTimeProvider { get; set; }
        public SessionManagerMock SessionManager { get; set; }

        public SessionOwnerMock CoordinationSessionOwner1 { get; set; }
        public SessionOwnerMock CoordinationSessionOwner2 { get; set; }
        public SessionOwnerMock CoordinationSessionOwner3 { get; set; }

        public IOptions<CoordinationManagerOptions> OptionsAccessor { get; set; }

        public LockWaitDirectory LockWaitDirectory1 { get; set; }
        public LockWaitDirectory LockWaitDirectory2 { get; set; }
        public LockWaitDirectory LockWaitDirectory3 { get; set; }

        public InvalidationCallbackDirectory InvalidationCallbackDirectory1 { get; set; }
        public InvalidationCallbackDirectory InvalidationCallbackDirectory2 { get; set; }
        public InvalidationCallbackDirectory InvalidationCallbackDirectory3 { get; set; }

        public CoordinationExchangeManager<TestMessagingSystemAddress> CoordinationExchangeManager1 { get; set; }
        public CoordinationExchangeManager<TestMessagingSystemAddress> CoordinationExchangeManager2 { get; set; }
        public CoordinationExchangeManager<TestMessagingSystemAddress> CoordinationExchangeManager3 { get; set; }

        private static byte[] SerializeAddress(TestMessagingSystemAddress address)
        {
            return Encoding.UTF8.GetBytes(address.RawAddress.ToString());
        }

        private static TestMessagingSystemAddress DeserializeAddress(byte[] bytes)
        {
            return new TestMessagingSystemAddress(int.Parse(Encoding.UTF8.GetString(bytes)));
        }

        [TestInitialize]
        public void Setup()
        {
            MessagingSystem = new TestMessagingSystem();
            PhysicalEndPoint1 = MessagingSystem.CreatePhysicalEndPoint();
            PhysicalEndPoint2 = MessagingSystem.CreatePhysicalEndPoint();
            PhysicalEndPoint3 = MessagingSystem.CreatePhysicalEndPoint();
            PhysicalEndPointMultiplexer1 = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(PhysicalEndPoint1);
            PhysicalEndPointMultiplexer2 = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(PhysicalEndPoint2);
            PhysicalEndPointMultiplexer3 = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(PhysicalEndPoint3);

            DateTimeProvider = new DateTimeProviderMock();
            SessionManager = new SessionManagerMock(DateTimeProvider);

            CoordinationSessionOwner1 = new SessionOwnerMock(
                new SessionIdentifier(ReadOnlySpan<byte>.Empty, SerializeAddress(PhysicalEndPointMultiplexer1.LocalAddress)));
            CoordinationSessionOwner2 = new SessionOwnerMock(
                new SessionIdentifier(ReadOnlySpan<byte>.Empty, SerializeAddress(PhysicalEndPointMultiplexer2.LocalAddress)));
            CoordinationSessionOwner3 = new SessionOwnerMock(
                new SessionIdentifier(ReadOnlySpan<byte>.Empty, SerializeAddress(PhysicalEndPointMultiplexer3.LocalAddress)));

            SessionManager.TryBeginSessionAsync(CoordinationSessionOwner1.Session, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30));
            SessionManager.TryBeginSessionAsync(CoordinationSessionOwner2.Session, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30));
            SessionManager.TryBeginSessionAsync(CoordinationSessionOwner3.Session, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30));

            OptionsAccessor = Options.Create(new CoordinationManagerOptions { });

            LockWaitDirectory1 = new LockWaitDirectory();
            LockWaitDirectory2 = new LockWaitDirectory();
            LockWaitDirectory3 = new LockWaitDirectory();

            InvalidationCallbackDirectory1 = new InvalidationCallbackDirectory();
            InvalidationCallbackDirectory2 = new InvalidationCallbackDirectory();
            InvalidationCallbackDirectory3 = new InvalidationCallbackDirectory();

            CoordinationExchangeManager1 = new CoordinationExchangeManager<TestMessagingSystemAddress>(
                CoordinationSessionOwner1, SessionManager, LockWaitDirectory1,
                InvalidationCallbackDirectory1, PhysicalEndPointMultiplexer1,
                OptionsAccessor);

            CoordinationExchangeManager2 = new CoordinationExchangeManager<TestMessagingSystemAddress>(
                CoordinationSessionOwner2, SessionManager, LockWaitDirectory2,
                InvalidationCallbackDirectory2, PhysicalEndPointMultiplexer2,
                OptionsAccessor);

            CoordinationExchangeManager3 = new CoordinationExchangeManager<TestMessagingSystemAddress>(
                CoordinationSessionOwner3, SessionManager, LockWaitDirectory3,
                InvalidationCallbackDirectory3, PhysicalEndPointMultiplexer3,
                OptionsAccessor);
        }

        [TestMethod]
        public async Task InvalidateCacheEntryTest()
        {
            var called1 = false;
            var called2 = false;
            var called3 = false;

            InvalidationCallbackDirectory1.Register("abc", _ =>
            {
                called1 = true;
                return default;
            });

            InvalidationCallbackDirectory2.Register("abc", _ =>
            {
                called2 = true;
                return default;
            });

            InvalidationCallbackDirectory3.Register("abc", _ =>
            {
                called3 = true;
                return default;
            });

            await CoordinationExchangeManager1.InvalidateCacheEntryAsync("abc", CoordinationSessionOwner2.Session, default);

            await Task.Delay(30);

            Assert.IsFalse(called1);
            Assert.IsTrue(called2);
            Assert.IsFalse(called3);
        }

        [TestMethod]
        public async Task NotifyReadLockReleasedTest()
        {
            var task1 = LockWaitDirectory1.WaitForReadLockNotificationAsync("abc", CoordinationSessionOwner1.Session, default).AsTask();
            var task2 = LockWaitDirectory1.WaitForReadLockNotificationAsync("abc", CoordinationSessionOwner1.Session, default).AsTask();
            var task3 = LockWaitDirectory1.WaitForReadLockNotificationAsync("abc", CoordinationSessionOwner1.Session, default).AsTask();

            await CoordinationExchangeManager1.NotifyReadLockReleasedAsync("abc", default);

            await Task.Delay(30);

            Assert.AreEqual(TaskStatus.RanToCompletion, task1.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, task2.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, task3.Status);
        }

        [TestMethod]
        public async Task NotifyWriteLockReleasedTest()
        {
            var task1 = LockWaitDirectory1.WaitForWriteLockNotificationAsync("abc", CoordinationSessionOwner1.Session, default).AsTask();
            var task2 = LockWaitDirectory1.WaitForWriteLockNotificationAsync("abc", CoordinationSessionOwner1.Session, default).AsTask();
            var task3 = LockWaitDirectory1.WaitForWriteLockNotificationAsync("abc", CoordinationSessionOwner1.Session, default).AsTask();

            await CoordinationExchangeManager1.NotifyWriteLockReleasedAsync("abc", default);

            await Task.Delay(30);

            Assert.AreEqual(TaskStatus.RanToCompletion, task1.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, task2.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, task3.Status);
        }

        // TODO: Test disposal
    }
}
