using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E
{
    [TestClass]
    public class TopologicalSortEnumerableExtensionTests
    {
        [TestMethod]
        public void SingleItemTest()
        {
            var item = new Item();
            var items = new[] { item };
            var orderedItems = items.TopologicalSort(p => p.Dependencies, true).ToList();

            Assert.AreEqual(1, orderedItems.Count);
            Assert.AreSame(item, orderedItems.First());
        }

        [TestMethod]
        public void DependentItemsTest()
        {
            var item1 = new Item();
            var item2 = new Item(item1);
            var items = new[] { item2, item1 };
            var orderedItems = items.TopologicalSort(p => p.Dependencies, true).ToList();

            Assert.AreEqual(2, orderedItems.Count);
            Assert.AreSame(item1, orderedItems[0]);
            Assert.AreSame(item2, orderedItems[1]);
        }

        [TestMethod]
        public void DependencyChainTest()
        {
            var item1 = new Item();
            var item2 = new Item(item1);
            var item3 = new Item(item2);
            var items = new[] { item2, item1, item3 };
            var orderedItems = items.TopologicalSort(p => p.Dependencies, true).ToList();

            Assert.AreEqual(3, orderedItems.Count);
            Assert.AreSame(item1, orderedItems[0]);
            Assert.AreSame(item2, orderedItems[1]);
            Assert.AreSame(item3, orderedItems[2]);
        }

        [TestMethod]
        public void StableOrderTest()
        {
            var item1 = new Item();
            var item2 = new Item(item1);
            var item3 = new Item(item1);
            var item4 = new Item(item1);
            var items = new[] { item2, item3, item1, item4 };
            var orderedItems = items.TopologicalSort(p => p.Dependencies, true).ToList();

            Assert.AreEqual(4, orderedItems.Count);
            Assert.AreSame(item1, orderedItems[0]);
            Assert.AreSame(item2, orderedItems[1]);
            Assert.AreSame(item3, orderedItems[2]);
            Assert.AreSame(item4, orderedItems[3]);
        }

        [TestMethod]
        public void ThrowsOnCycleTest()
        {
            var item1 = new Item();
            var item2 = new Item(item1);
            item1.Dependencies.Add(item2);
            var items = new[] { item2, item1 };

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                items.TopologicalSort(p => p.Dependencies, true);
            });
        }
    }

    public class Item
    {
        public Item() { }

        public Item(params Item[] dependencies)
        {
            Dependencies.AddRange(dependencies);
        }

        public List<Item> Dependencies { get; } = new List<Item>();
    }
}
