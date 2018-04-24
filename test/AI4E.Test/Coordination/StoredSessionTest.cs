using System;
using System.Linq;
using AI4E.Coordination;
using AI4E.Test.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Coordination
{
    [TestClass]
    public sealed class StoredSessionTest
    {
        public StoredSessionTest()
        {

        }

        [TestMethod]
        public void ConstructionTest()
        {
            var dateTimeProvider = new DebugDateTimeProvider();
            var storedSessionManager = new StoredSessionManager(dateTimeProvider);
            var leaseLength = TimeSpan.FromSeconds(1);
            var key = "mysession";
            var leaseEnd = dateTimeProvider.CurrentTime + leaseLength;
            var storedSession = storedSessionManager.Begin(key, leaseEnd);

            Assert.AreEqual(key, storedSession.Key);
            Assert.AreEqual(leaseEnd, storedSession.LeaseEnd);
            Assert.AreEqual(1, storedSession.StorageVersion);
            Assert.IsNotNull(storedSession.Entries);
            Assert.AreEqual(0, storedSession.Entries.Count());
            Assert.IsFalse(storedSessionManager.IsEnded(storedSession));

            dateTimeProvider.CurrentTime = leaseEnd;

            Assert.IsTrue(storedSessionManager.IsEnded(storedSession));
        }

        [TestMethod]
        public void EndTest()
        {
            var dateTimeProvider = new DebugDateTimeProvider();
            var storedSessionManager = new StoredSessionManager(dateTimeProvider);
            var leaseLength = TimeSpan.FromSeconds(1);
            var key = "mysession";
            var leaseEnd = dateTimeProvider.CurrentTime + leaseLength;
            var storedSession = storedSessionManager.Begin(key, leaseEnd);

            storedSession = storedSessionManager.End(storedSession);

            Assert.AreEqual(key, storedSession.Key);
            Assert.AreEqual(leaseEnd, storedSession.LeaseEnd);
            Assert.AreEqual(2, storedSession.StorageVersion);
            Assert.IsNotNull(storedSession.Entries);
            Assert.AreEqual(0, storedSession.Entries.Count());
            Assert.IsTrue(storedSessionManager.IsEnded(storedSession));
        }

        [TestMethod]
        public void EndEndedSessionTest()
        {
            var dateTimeProvider = new DebugDateTimeProvider();
            var storedSessionManager = new StoredSessionManager(dateTimeProvider);
            var leaseLength = TimeSpan.FromSeconds(1);
            var key = "mysession";
            var leaseEnd = dateTimeProvider.CurrentTime + leaseLength;
            var storedSession = storedSessionManager.Begin(key, leaseEnd);

            // Lease ended => The session is ended
            dateTimeProvider.CurrentTime = leaseEnd;

            // The session is already ended => This shouls be a nop
            storedSession = storedSessionManager.End(storedSession);

            Assert.AreEqual(1, storedSession.StorageVersion);
            Assert.IsTrue(storedSessionManager.IsEnded(storedSession));
        }

        [TestMethod]
        public void EndEndedSessionTest2()
        {
            var dateTimeProvider = new DebugDateTimeProvider();
            var storedSessionManager = new StoredSessionManager(dateTimeProvider);
            var leaseLength = TimeSpan.FromSeconds(1);
            var key = "mysession";
            var leaseEnd = dateTimeProvider.CurrentTime + leaseLength;
            var storedSession = storedSessionManager.Begin(key, leaseEnd);

            // End the session
            storedSession = storedSessionManager.End(storedSession);

            // The session is already ended => This shouls be a nop
            storedSession = storedSessionManager.End(storedSession);

            Assert.AreEqual(2, storedSession.StorageVersion);
            Assert.IsTrue(storedSessionManager.IsEnded(storedSession));
        }

        [TestMethod]
        public void UpdateLeaseTest()
        {
            var dateTimeProvider = new DebugDateTimeProvider();
            var storedSessionManager = new StoredSessionManager(dateTimeProvider);
            var leaseLength = TimeSpan.FromSeconds(1);
            var leaseLengthHalf = new TimeSpan(leaseLength.Ticks / 2);
            var key = "mysession";

            var updateTime = dateTimeProvider.CurrentTime + leaseLengthHalf;
            var leaseEndBeforeUpdate = dateTimeProvider.CurrentTime + leaseLength;
            var leaseEndAfterUpdate = updateTime + leaseLength;

            var storedSession = storedSessionManager.Begin(key, leaseEndBeforeUpdate);

            dateTimeProvider.CurrentTime = updateTime;


            // We update the lease after a half lease length
            storedSession = storedSessionManager.UpdateLease(storedSession, leaseEndAfterUpdate);

            // Our time is the end of the original lease, before the update
            dateTimeProvider.CurrentTime = leaseEndBeforeUpdate;

            Assert.IsFalse(storedSessionManager.IsEnded(storedSession));

            // Our time is the end the updatd lease. The session should be ended now.
            dateTimeProvider.CurrentTime = leaseEndAfterUpdate;

            Assert.IsTrue(storedSessionManager.IsEnded(storedSession));
            Assert.AreEqual(2, storedSession.StorageVersion);

            // This should be a nop
            storedSession = storedSessionManager.UpdateLease(storedSession, leaseEndAfterUpdate + leaseLength);

            Assert.IsTrue(storedSessionManager.IsEnded(storedSession));
            Assert.AreEqual(2, storedSession.StorageVersion);
        }

        [TestMethod]
        public void DecreaseLeaseTest()
        {
            var dateTimeProvider = new DebugDateTimeProvider();
            var storedSessionManager = new StoredSessionManager(dateTimeProvider);
            var leaseLength = TimeSpan.FromSeconds(1);
            var leaseLengthHalf = new TimeSpan(leaseLength.Ticks / 2);
            var key = "mysession";
            var leaseEnd = dateTimeProvider.CurrentTime + leaseLength;
            var updateTime = dateTimeProvider.CurrentTime + leaseLengthHalf;
            var storedSession = storedSessionManager.Begin(key, leaseEnd);

            storedSession = storedSessionManager.UpdateLease(storedSession, updateTime);

            Assert.AreEqual(1, storedSession.StorageVersion);
            Assert.AreEqual(leaseEnd, storedSession.LeaseEnd);
        }
    }
}
