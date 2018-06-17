//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using AI4E.Domain;
//using AI4E.Storage.Domain;
//using Microsoft.VisualStudio.TestTools.UnitTesting;

//namespace AI4E.Test.Storage
//{
//    [TestClass]
//    public class DefaultEntityAccessorTest
//    {
//        [TestMethod]
//        public void ConstructionTest()
//        {
//            var accessor = GetAccessor();
//        }

//        private static IEntityAccessor GetAccessor()
//        {
//            return new DefaultEntityAccessor();
//        }

//        [TestMethod]
//        public void GetIdTest()
//        {
//            var entity = new MyEntity(Guid.NewGuid());

//            var accessor = GetAccessor();

//            var id = accessor.GetId(entity);

//            Assert.AreEqual(entity.Id.ToString(), id);
//        }

//        [TestMethod]
//        public void GetConcurrencyTokenTest()
//        {
//            var entity = new MyEntity(Guid.NewGuid());
//            entity.SetConcurrencyToken(Guid.NewGuid());

//            var accessor = GetAccessor();
//            var concurrencyToken = accessor.GetConcurrencyToken(entity);

//            Assert.AreEqual(entity.ConcurrencyToken, concurrencyToken);
//        }

//        [TestMethod]
//        public void SetConcurrencyTokenTest()
//        {
//            var entity = new MyEntity(Guid.NewGuid());
//            var accessor = GetAccessor();
//            var concurrencyToken = Guid.NewGuid().ToString();

//            accessor.SetConcurrencyToken(entity, concurrencyToken);

//            Assert.AreEqual(concurrencyToken, entity.ConcurrencyToken);
//        }

//        [TestMethod]
//        public void GetRevision()
//        {
//            var entity = new MyEntity(Guid.NewGuid());
//            entity.SetRevision(248L);

//            var accessor = GetAccessor();
//            var revision = accessor.GetRevision(entity);

//            Assert.AreEqual(entity.Revision, revision);
//        }

//        [TestMethod]
//        public void SetRevisionTest()
//        {
//            var entity = new MyEntity(Guid.NewGuid());
//            var accessor = GetAccessor();
//            var revision = 5896L;

//            accessor.SetRevision(entity, revision);

//            Assert.AreEqual(revision, entity.Revision);
//        }

//        [TestMethod]
//        public void GetUncommittedEventsTest()
//        {
//            var entity = new MyEntity(Guid.NewGuid());
//            var accessor = GetAccessor();

//            var uncommittedEvents = accessor.GetUncommittedEvents(entity);

//            Assert.AreEqual(entity.GetUncommittedEvents(), uncommittedEvents);

//            entity.Publish(new MyDomainEvent1(entity.Id));
//            entity.Publish(new MyDomainEvent2(entity.Id));

//            uncommittedEvents = accessor.GetUncommittedEvents(entity);

//            Assert.AreEqual(entity.GetUncommittedEvents(), uncommittedEvents);
//        }

//        [TestMethod]
//        public void CommitEventsTest()
//        {
//            var entity = new MyEntity(Guid.NewGuid());
//            var accessor = GetAccessor();

//            accessor.CommitEvents(entity);

//            Assert.AreEqual(0, entity.GetUncommittedEvents().Count());

//            entity.Publish(new MyDomainEvent1(entity.Id));
//            entity.Publish(new MyDomainEvent2(entity.Id));

//            accessor.CommitEvents(entity);

//            Assert.AreEqual(0, entity.GetUncommittedEvents().Count());
//        }
//    }

//    public class MyEntity : AggregateRoot
//    {
//        public MyEntity(Guid id) : base(id)
//        {

//        }

//        public IEnumerable<DomainEvent> GetUncommittedEvents()
//        {
//            var property = typeof(AggregateRoot).GetProperty("UncommittedEvents", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
//            return (IEnumerable<DomainEvent>)property.GetValue(this);
//        }

//        public void SetConcurrencyToken(Guid concurrencyToken)
//        {
//            var property = typeof(AggregateRoot).GetProperty("ConcurrencyToken", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
//            property.SetValue(this, concurrencyToken.ToString());
//        }

//        public void SetRevision(long revision)
//        {
//            var property = typeof(AggregateRoot).GetProperty("Revision", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
//            property.SetValue(this, revision);
//        }

//        public new void Publish<T>(T domainEvent)
//            where T : DomainEvent
//        {
//            base.Publish(domainEvent);
//        }
//    }

//    public class MyDomainEvent1 : DomainEvent
//    {
//        public MyDomainEvent1(Guid stream) : base(stream)
//        {

//        }
//    }

//    public class MyDomainEvent2 : DomainEvent
//    {
//        public MyDomainEvent2(Guid stream) : base(stream)
//        {

//        }
//    }
//}
