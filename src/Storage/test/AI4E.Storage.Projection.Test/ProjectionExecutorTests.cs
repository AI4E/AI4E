using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Projection.TestTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Projection
{
    [TestClass]
    public sealed class ProjectionExecutorTests
    {
        [TestMethod]
        public async Task BasicTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var projectionRegistry = new ProjectionRegistry();
            projectionRegistry.Register(
                new ProjectionRegistration(
                    typeof(ProjectionSource), typeof(ProjectionTarget), provider => new SourceProjection()));

            var executor = new ProjectionExecutor(projectionRegistry);
            var source = new ProjectionSource("abc");

            var targets = await executor.ExecuteProjectionAsync(
                typeof(ProjectionSource), source, serviceProvider, cancellation: default).ToListAsync();

            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual(typeof(ProjectionTarget), targets[0].ResultType);
            Assert.AreEqual(typeof(ProjectionTarget), targets[1].ResultType);
            Assert.IsInstanceOfType(targets[0].Result, typeof(ProjectionTarget));
            Assert.IsInstanceOfType(targets[1].Result, typeof(ProjectionTarget));
            Assert.AreSame(source, (targets[0].Result as ProjectionTarget).Source);
            Assert.AreSame(source, (targets[1].Result as ProjectionTarget).Source);
        }

        [TestMethod]
        public async Task SourceInheritanceTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var projectionRegistry = new ProjectionRegistry();
            projectionRegistry.Register(
                new ProjectionRegistration(
                    typeof(object), typeof(ProjectionTarget), provider => new ObjectProjection()));

            var executor = new ProjectionExecutor(projectionRegistry);
            var source = new ProjectionSource("abc");

            var targets = await executor.ExecuteProjectionAsync(
                typeof(ProjectionSource), source, serviceProvider, cancellation: default).ToListAsync();

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(typeof(ProjectionTarget), targets[0].ResultType);
            Assert.IsInstanceOfType(targets[0].Result, typeof(ProjectionTarget));
            Assert.AreSame(source, (targets[0].Result as ProjectionTarget).Source);
        }

        [TestMethod]
        public async Task MultipleProjectionsTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var projectionRegistry = new ProjectionRegistry();
            projectionRegistry.Register(
                new ProjectionRegistration(
                    typeof(object), typeof(ProjectionTarget), provider => new ObjectProjection()));
            projectionRegistry.Register(
               new ProjectionRegistration(
                   typeof(ProjectionSource), typeof(string), provider => new ToStringProjection()));

            var executor = new ProjectionExecutor(projectionRegistry);
            var source = new ProjectionSource("abc");

            var targets = await executor.ExecuteProjectionAsync(
                typeof(ProjectionSource), source, serviceProvider, cancellation: default).ToListAsync();

            Assert.AreEqual(2, targets.Count);
        }

        [TestMethod]
        public async Task NoMatchingProjectionsTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var projectionRegistry = new ProjectionRegistry();
            projectionRegistry.Register(
                new ProjectionRegistration(
                    typeof(string), typeof(ProjectionTarget), provider => new StringProjection()));

            var executor = new ProjectionExecutor(projectionRegistry);
            var source = new ProjectionSource("abc");

            var targets = await executor.ExecuteProjectionAsync(
                typeof(ProjectionSource), source, serviceProvider, cancellation: default).ToListAsync();

            Assert.AreEqual(0, targets.Count);
        }

        private sealed class ToStringProjection : IProjection<ProjectionSource, string>
        {
            public IAsyncEnumerable<string> ProjectAsync(ProjectionSource source, CancellationToken cancellation = default)
            {
                return AsyncEnumerable.Repeat(source.Value, 1);
            }
        }

        private sealed class SourceProjection : IProjection<ProjectionSource, ProjectionTarget>
        {
            public IAsyncEnumerable<ProjectionTarget> ProjectAsync(ProjectionSource source, CancellationToken cancellation = default)
            {
                return new List<ProjectionTarget>
                {
                    new ProjectionTarget(source),
                    new ProjectionTarget(source)
                }.ToAsyncEnumerable();
            }
        }

        private sealed class ObjectProjection : IProjection<object, ProjectionTarget>
        {
            public IAsyncEnumerable<ProjectionTarget> ProjectAsync(object source, CancellationToken cancellation = default)
            {
                return AsyncEnumerable.Repeat(new ProjectionTarget(source as ProjectionSource), 1);
            }
        }

        private sealed class StringProjection : IProjection<string, ProjectionTarget>
        {
            public IAsyncEnumerable<ProjectionTarget> ProjectAsync(string source, CancellationToken cancellation = default)
            {
                throw null;
            }
        }
    }
}
