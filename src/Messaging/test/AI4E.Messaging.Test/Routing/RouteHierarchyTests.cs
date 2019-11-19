using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.Routing
{
    [TestClass]
    public sealed class RouteHierarchyTests
    {
        private static readonly List<Route> _testRoutes = new List<Route>
        {
            new Route(typeof(object)),
            new Route(typeof(Exception)),
            new Route(typeof(ArgumentException))
        };

        private static RouteHierarchy BuildTestRouteHierarchy()
        {
            return new RouteHierarchy(_testRoutes);
        }

        [TestMethod]
        public void EnumerateTest()
        {
            var routeHierarchy = BuildTestRouteHierarchy();

            var index = 0;
            foreach (var route in routeHierarchy)
            {
                Assert.AreEqual(_testRoutes[index], route);
                index++;
            }
        }

        [TestMethod]
        public void EnumerableTest()
        {
            var routes = BuildTestRouteHierarchy().ToList();
            Assert.IsTrue(routes.SequenceEqual(_testRoutes));

        }
    }
}
