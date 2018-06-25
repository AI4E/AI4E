using System;
using AI4E.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Storage
{
    [TestClass]
    public class DefaultMessageAccessorTest
    {
        [TestMethod]
        public void ConstructionTest()
        {
            var accessor = new DefaultMessageAccessor();
        }

        [TestMethod]
        public void AccessingNoneTest()
        {
            var accessor = new DefaultMessageAccessor();
            var message = new EmptyMessage();

            Assert.IsFalse(accessor.TryGetEntityId(message, out _));
            Assert.IsFalse(accessor.TryGetConcurrencyToken(message, out _));
        }

        [TestMethod]
        public void AccessingIdOnlyTest()
        {
            var id = Guid.NewGuid();

            var accessor = new DefaultMessageAccessor();
            var message = new MessageWithIdOnly(id);

            Assert.IsTrue(accessor.TryGetEntityId(message, out var aId));
            Assert.IsFalse(accessor.TryGetConcurrencyToken(message, out _));
            Assert.AreEqual(id.ToString(), aId);
        }

        [TestMethod]
        public void AccessingBothTest()
        {
            var id = Guid.NewGuid();
            var concurrencyToken = Guid.NewGuid().ToString();

            var accessor = new DefaultMessageAccessor();
            var message = new MessageWithIdAndConcurrencyToken(id, concurrencyToken);

            Assert.IsTrue(accessor.TryGetEntityId(message, out var aId));
            Assert.IsTrue(accessor.TryGetConcurrencyToken(message, out var aConcurrencyToken));
            Assert.AreEqual(id.ToString(), aId);
            Assert.AreEqual(concurrencyToken, aConcurrencyToken);
        }

        [TestMethod]
        public void AccessingNonPublicMembersTest()
        {
            var id = Guid.NewGuid();
            var concurrencyToken = Guid.NewGuid().ToString();

            var accessor = new DefaultMessageAccessor();
            var message = new MessageWithNonPublicMembers(id, concurrencyToken);

            Assert.IsTrue(accessor.TryGetEntityId(message, out var aId));
            Assert.IsTrue(accessor.TryGetConcurrencyToken(message, out var aConcurrencyToken));
            Assert.AreEqual(id.ToString(), aId);
            Assert.AreEqual(concurrencyToken, aConcurrencyToken);
        }

        //[TestMethod]
        //public void AccessingWithWrongIdTypeTest()
        //{
        //    var id = Guid.NewGuid();
        //    var concurrencyToken = Guid.NewGuid().ToString();

        //    var accessor = new DefaultMessageAccessor();
        //    var message = new MessageWithIdAndConcurrencyToken(id, concurrencyToken);

        //    Assert.IsFalse(accessor.TryGetEntityId(message, out _));
        //    Assert.IsTrue(accessor.TryGetConcurrencyToken(message, out var aConcurrencyToken));
        //    Assert.AreEqual(concurrencyToken, aConcurrencyToken);
        //}

        [TestMethod]
        public void AccessingCommandTest()
        {
            var id = Guid.NewGuid();
            var concurrencyToken = Guid.NewGuid().ToString();

            var accessor = new DefaultMessageAccessor();
            var message = new TestCommand(id, concurrencyToken);

            Assert.IsTrue(accessor.TryGetEntityId(message, out var aId));
            Assert.IsTrue(accessor.TryGetConcurrencyToken(message, out var aConcurrencyToken));
            Assert.AreEqual(id.ToString(), aId);
            Assert.AreEqual(concurrencyToken, aConcurrencyToken);
        }

        public class EmptyMessage { }

        public class MessageWithIdOnly
        {
            public MessageWithIdOnly(Guid id)
            {
                Id = id;
            }

            public Guid Id { get; }
        }

        public class MessageWithIdAndConcurrencyToken
        {
            public MessageWithIdAndConcurrencyToken(Guid id, string concurrencyToken)
            {
                Id = id;
                ConcurrencyToken = concurrencyToken;
            }

            public Guid Id { get; }

            public string ConcurrencyToken { get; }
        }

        public class MessageWithNonPublicMembers
        {
            public MessageWithNonPublicMembers(Guid id, string concurrencyToken)
            {
                Id = id;
                ConcurrencyToken = concurrencyToken;
            }

            internal Guid Id { get; }

            internal string ConcurrencyToken { get; }
        }

        public class TestCommand : ConcurrencySafeCommand
        {
            public TestCommand(Guid id, string concurrencyToken) : base(id, concurrencyToken) { }
        }
    }
}
