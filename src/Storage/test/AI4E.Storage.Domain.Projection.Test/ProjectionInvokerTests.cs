using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Domain.Projection.TestTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Domain.Projection
{
    [TestClass]
    public sealed class ProjectionInvokerTests
    {
        [TestMethod]
        public void BuildTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectResolveServices)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            Assert.AreEqual(typeof(ProjectionSource), (projection as IProjection).SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), (projection as IProjection).TargetType);
        }

        [TestMethod]
        public void CreateInvokerTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectResolveServices)),
                multipleResults: false, projectNonExisting: false);
            var projection = ProjectionInvoker.CreateInvoker(projectionDescriptor, serviceProvider);

            Assert.IsInstanceOfType(projection, typeof(ProjectionInvoker<ProjectionSource, ProjectionTarget>));
            Assert.AreEqual(typeof(ProjectionSource), (projection as IProjection).SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), (projection as IProjection).TargetType);
        }

        [TestMethod]
        public async Task ResolveServicesTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectResolveServices)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(source, handler.ProjectionSource);
            Assert.IsTrue(cancellationToken.Equals(handler.Cancellation));
            Assert.AreSame(serviceProvider.GetRequiredService<IService>(), handler.Service);
        }

        [TestMethod]
        public async Task ResolveUnresolvableRequiredServiceTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectResolveUnresolvableRequiredService)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await projection.ProjectAsync(source, cancellationToken);
            });
        }

        [TestMethod]
        public async Task ResolveUnresolvableNonRequiredServiceTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectResolveUnresolvableNonRequiredService)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            await projection.ProjectAsync(source, cancellationToken);

            Assert.IsNull(handler.Service);
        }

        [TestMethod]
        public async Task SingleResultTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectSingleResult)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(1, targets.Count());
            Assert.AreSame(source, targets.First().Source);
        }

        [TestMethod]
        public async Task SingleResultAsyncTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectSingleResultAsync)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(1, targets.Count());
            Assert.AreSame(source, targets.First().Source);
        }

        [TestMethod]
        public async Task MultipleResultTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectMultipleResult)),
                multipleResults: true, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(1, targets.Count());
            Assert.AreSame(source, targets.First().Source);
        }

        [TestMethod]
        public async Task MultipleResultAsyncTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectMultipleResultAsync)),
                multipleResults: true, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(1, targets.Count());
            Assert.AreSame(source, targets.First().Source);
        }

        [TestMethod]
        public async Task MultipleResultAsAsyncEnumerableTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectMultipleResultAsAsyncEnumerable)),
                multipleResults: true, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(1, targets.Count());
            Assert.AreSame(source, targets.First().Source);
        }

        [TestMethod]
        public async Task MultipleResultAsyncAsAsyncEnumerableTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectMultipleResultAsyncAsAsyncEnumerable)),
                multipleResults: true, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(1, targets.Count());
            Assert.AreSame(source, targets.First().Source);
        }

        [TestMethod]
        public async Task SingleResultCastedTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectSingleResultCasted)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.AreEqual(1, targets.Count());
            Assert.AreSame(source, targets.First().Source);
        }

        [TestMethod]
        public async Task ThrowTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectThrow)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = new ProjectionSource();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            await Assert.ThrowsExceptionAsync<EntryPointNotFoundException>(async () =>
            {
                await projection.ProjectAsync(source, cancellationToken);
            });
        }

        [TestMethod]
        public async Task ProjectNonExistingTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectSingleResult)),
                multipleResults: false, projectNonExisting: true);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = default(ProjectionSource);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.IsTrue(handler.Invoked);
            Assert.AreEqual(1, targets.Count());
            Assert.IsNull(targets.First().Source);
            Assert.IsNull(handler.ProjectionSource);
        }

        [TestMethod]
        public async Task ProjectNonExistingNonInvokationTest()
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var projectionDescriptor = new ProjectionDescriptor(
                typeof(TestProjection),
                typeof(ProjectionSource),
                typeof(ProjectionTarget),
                typeof(TestProjection).GetMethod(nameof(TestProjection.ProjectSingleResult)),
                multipleResults: false, projectNonExisting: false);
            var handler = new TestProjection();
            var projection = new ProjectionInvoker<ProjectionSource, ProjectionTarget>(handler, projectionDescriptor, serviceProvider);

            var source = default(ProjectionSource);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var targets = await projection.ProjectAsync(source, cancellationToken);

            Assert.IsFalse(handler.Invoked);
            Assert.AreEqual(0, targets.Count());
            Assert.IsNull(handler.ProjectionSource);
        }
    }

    public sealed class TestProjection
    {
        public ProjectionTarget ProjectResolveServices(
            ProjectionSource projectionSource,
            CancellationToken cancellation,
            IServiceProvider serviceProvider,
            IService service)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            Cancellation = cancellation;
            ServiceProvider = serviceProvider;
            Service = service;

            return new ProjectionTarget(projectionSource);
        }

        public ProjectionTarget ProjectResolveUnresolvableRequiredService(ProjectionSource projectionSource, IService service)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            Service = service;

            return new ProjectionTarget(projectionSource);
        }

        public ProjectionTarget ProjectResolveUnresolvableNonRequiredService(ProjectionSource projectionSource, IService service = null)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            Service = service;

            return new ProjectionTarget(projectionSource);
        }

        public ProjectionTarget ProjectSingleResult(ProjectionSource projectionSource)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            return new ProjectionTarget(projectionSource);
        }

        public ValueTask<ProjectionTarget> ProjectSingleResultAsync(ProjectionSource projectionSource)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            return new ValueTask<ProjectionTarget>(new ProjectionTarget(projectionSource));
        }

        public List<ProjectionTarget> ProjectMultipleResult(ProjectionSource projectionSource)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            return new List<ProjectionTarget> { new ProjectionTarget(projectionSource) };
        }

        public ValueTask<List<ProjectionTarget>> ProjectMultipleResultAsync(ProjectionSource projectionSource)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            return new ValueTask<List<ProjectionTarget>>(new List<ProjectionTarget> { new ProjectionTarget(projectionSource) });
        }

        public IAsyncEnumerable<ProjectionTarget> ProjectMultipleResultAsAsyncEnumerable(ProjectionSource projectionSource)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            return new AsyncEnumerableWrapper(new List<ProjectionTarget> { new ProjectionTarget(projectionSource) }.ToAsyncEnumerable());
        }

        public ValueTask<IAsyncEnumerable<ProjectionTarget>> ProjectMultipleResultAsyncAsAsyncEnumerable(ProjectionSource projectionSource)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            return
                new ValueTask<IAsyncEnumerable<ProjectionTarget>>(
                    new AsyncEnumerableWrapper(
                        new List<ProjectionTarget>
                        {
                            new ProjectionTarget(projectionSource)
                        }.ToAsyncEnumerable()));
        }

        public object ProjectSingleResultCasted(ProjectionSource projectionSource)
        {
            Invoked = true;
            ProjectionSource = projectionSource;
            return new ProjectionTarget(projectionSource);
        }

#pragma warning disable IDE0060
        public ProjectionTarget ProjectThrow(ProjectionSource projectionSource)
#pragma warning restore IDE0060
        {
            Invoked = true;
            throw new EntryPointNotFoundException();
        }

        public bool Invoked { get; set; }
        public ProjectionSource ProjectionSource { get; set; }
        public CancellationToken Cancellation { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public IService Service { get; set; }

        private sealed class AsyncEnumerableWrapper : IAsyncEnumerable<ProjectionTarget>
        {
            private readonly IAsyncEnumerable<ProjectionTarget> _asyncEnumerable;

            public AsyncEnumerableWrapper(IAsyncEnumerable<ProjectionTarget> asyncEnumerable)
            {
                _asyncEnumerable = asyncEnumerable;
            }

            public IAsyncEnumerator<ProjectionTarget> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return _asyncEnumerable.GetAsyncEnumerator(cancellationToken);
            }
        }
    }

    public interface IService { }

    public class Service : IService { }
}
