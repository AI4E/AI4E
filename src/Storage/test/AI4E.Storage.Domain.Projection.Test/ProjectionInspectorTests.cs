using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Storage.Domain.Projection.TestTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Domain.Projection
{
    [TestClass]
    public sealed class ProjectionInspectorTests
    {
        [TestMethod]
        public void SyncHandlerTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(SyncProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.AreEqual(typeof(SyncProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(SyncProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void AsyncProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(AsyncProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.AreEqual(typeof(AsyncProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(AsyncProjection).GetMethod("ProjectAsync"), descriptor.Member);
        }

        [TestMethod]
        public void SuffixSyncProjectionTest()
        {
            Assert.AreEqual(0, ProjectionInspector.Instance.InspectType(typeof(SuffixSyncProjection)).Count());
        }

        [TestMethod]
        public void MissingSuffixAsyncProjectionTest()
        {
            Assert.AreEqual(0, ProjectionInspector.Instance.InspectType(typeof(MissingSuffixAsyncProjection)).Count());
        }

        [TestMethod]
        public void WithRefParamProjectionTest()
        {
            Assert.AreEqual(0, ProjectionInspector.Instance.InspectType(typeof(WithRefParamProjection)).Count());
        }

        [TestMethod]
        public void GenericActionProjectionTest()
        {
            Assert.AreEqual(0, ProjectionInspector.Instance.InspectType(typeof(GenericActionProjection)).Count());
        }

        [TestMethod]
        public void EmptyParametersProjectionTest()
        {
            Assert.AreEqual(0, ProjectionInspector.Instance.InspectType(typeof(EmptyParametersProjection)).Count());
        }

        [TestMethod]
        public void NoActionAttributeProjectionTest()
        {
            Assert.AreEqual(0, ProjectionInspector.Instance.InspectType(typeof(NoActionAttributeProjection)).Count());
        }

        [TestMethod]
        public void SuffixSyncWithActionAttributeProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(SuffixSyncWithActionAttributeProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.AreEqual(typeof(SuffixSyncWithActionAttributeProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(SuffixSyncWithActionAttributeProjection).GetMethod("ProjectAsync"), descriptor.Member);
        }

        [TestMethod]
        public void MissingSuffixAsyncWithActionAttributeProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(MissingSuffixAsyncWithActionAttributeProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.AreEqual(typeof(MissingSuffixAsyncWithActionAttributeProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(MissingSuffixAsyncWithActionAttributeProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithExplicitTypeProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithExplicitTypeProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(object), descriptor.TargetType);
            Assert.AreEqual(typeof(WithExplicitTypeProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithExplicitTypeProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithExplicitClassTypeProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithExplicitClassTypeProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(object), descriptor.TargetType);
            Assert.AreEqual(typeof(WithExplicitClassTypeProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithExplicitClassTypeProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithInvalidExplicitSourceTypeProjectionTest()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                ProjectionInspector.Instance.InspectType(typeof(WithInvalidExplicitSourceTypeProjection));
            });
        }

        [TestMethod]
        public void WithInvalidExplicitClassSourceTypeProjectionTest()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                ProjectionInspector.Instance.InspectType(typeof(WithInvalidExplicitClassSourceTypeProjection));
            });
        }

        [TestMethod]
        public void WithInvalidExplicitTargetTypeProjectionTest()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                ProjectionInspector.Instance.InspectType(typeof(WithInvalidExplicitTargetTypeProjection));
            });
        }

        [TestMethod]
        public void WithInvalidExplicitClassTargetTypeProjectionTest()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                ProjectionInspector.Instance.InspectType(typeof(WithInvalidExplicitClassTargetTypeProjection));
            });
        }

        [TestMethod]
        public void WithMultipleResultProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithMultipleResultProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.IsTrue(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithMultipleResultProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithMultipleResultProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithMultipleResultAsyncProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithMultipleResultAsyncProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.IsTrue(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithMultipleResultAsyncProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithMultipleResultAsyncProjection).GetMethod("ProjectAsync"), descriptor.Member);
        }

        [TestMethod]
        public void WithMultipleResultAsyncEnumerableProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithMultipleResultAsyncEnumerableProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.IsTrue(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithMultipleResultAsyncEnumerableProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithMultipleResultAsyncEnumerableProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithMultipleResultAsyncAsyncEnumerableProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithMultipleResultAsyncAsyncEnumerableProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.IsTrue(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithMultipleResultAsyncAsyncEnumerableProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithMultipleResultAsyncAsyncEnumerableProjection).GetMethod("ProjectAsync"), descriptor.Member);
        }


        [TestMethod]
        public void WithEnumerableResultExplicitNoMultipleResultProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithEnumerableResultExplicitNoMultipleResultProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(IEnumerable<ProjectionTarget>), descriptor.TargetType);
            Assert.IsFalse(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithEnumerableResultExplicitNoMultipleResultProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithEnumerableResultExplicitNoMultipleResultProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithEnumerableResultExplicitClassNoMultipleResultProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithEnumerableResultExplicitClassNoMultipleResultProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(IEnumerable<ProjectionTarget>), descriptor.TargetType);
            Assert.IsFalse(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithEnumerableResultExplicitClassNoMultipleResultProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithEnumerableResultExplicitClassNoMultipleResultProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithInvalidExplicitMultipleResultProjectionTest()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                ProjectionInspector.Instance.InspectType(typeof(WithInvalidExplicitMultipleResultProjection));
            });
        }

        [TestMethod]
        public void WithExplicitMultipleResultProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithExplicitMultipleResultProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.IsTrue(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithExplicitMultipleResultProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithExplicitMultipleResultProjection).GetMethod("Project"), descriptor.Member);
        }

        [TestMethod]
        public void WithExplicitMultipleResultAsyncProjectionTest()
        {
            var descriptor = ProjectionInspector.Instance.InspectType(typeof(WithExplicitMultipleResultAsyncProjection)).Single();

            Assert.AreEqual(typeof(ProjectionSource), descriptor.SourceType);
            Assert.AreEqual(typeof(ProjectionTarget), descriptor.TargetType);
            Assert.IsTrue(descriptor.MultipleResults);
            Assert.AreEqual(typeof(WithExplicitMultipleResultAsyncProjection), descriptor.HandlerType);
            Assert.AreEqual(typeof(WithExplicitMultipleResultAsyncProjection).GetMethod("ProjectAsync"), descriptor.Member);
        }
    }

#pragma warning disable IDE0060

    public sealed class SyncProjection
    {
        public ProjectionTarget Project(ProjectionSource x, int y)

        {
            throw null;
        }
    }

    public sealed class AsyncProjection
    {
        public Task<ProjectionTarget> ProjectAsync(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class SuffixSyncProjection
    {
        public ProjectionTarget ProjectAsync(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class MissingSuffixAsyncProjection
    {
        public Task<ProjectionTarget> Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithRefParamProjection
    {
        public ProjectionTarget Project(ProjectionSource x, ref int y)
        {
            throw null;
        }
    }

    public sealed class GenericActionProjection
    {
        public ProjectionTarget Project<T>(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class EmptyParametersProjection
    {
        public ProjectionTarget Project()
        {
            throw null;
        }
    }

    public sealed class NoActionAttributeProjection
    {
        [NoProjection]
        public ProjectionTarget Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class SuffixSyncWithActionAttributeProjection
    {
        [Projection]
        public ProjectionTarget ProjectAsync(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class MissingSuffixAsyncWithActionAttributeProjection
    {
        [Projection]
        public Task<ProjectionTarget> Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithExplicitTypeProjection
    {
        [Projection(SourceType = typeof(ProjectionSource), TargetType = typeof(object))]
        public ProjectionTarget Project(object x, int y)
        {
            throw null;
        }
    }

    [Projection(SourceType = typeof(ProjectionSource), TargetType = typeof(object))]
    public sealed class WithExplicitClassTypeProjection
    {
        public ProjectionTarget Project(object x, int y)
        {
            throw null;
        }
    }

    public sealed class WithInvalidExplicitSourceTypeProjection
    {
        [Projection(SourceType = typeof(WithInvalidExplicitSourceTypeProjection))]
        public ProjectionTarget Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    [Projection(SourceType = typeof(WithInvalidExplicitSourceTypeProjection))]
    public sealed class WithInvalidExplicitClassSourceTypeProjection
    {
        public ProjectionTarget Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithInvalidExplicitTargetTypeProjection
    {
        [Projection(TargetType = typeof(string))]
        public ProjectionTarget Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    [Projection(TargetType = typeof(string))]
    public sealed class WithInvalidExplicitClassTargetTypeProjection
    {
        public ProjectionTarget Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithMultipleResultProjection
    {
        public IEnumerable<ProjectionTarget> Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithMultipleResultAsyncProjection
    {
        public Task<IEnumerable<ProjectionTarget>> ProjectAsync(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithMultipleResultAsyncEnumerableProjection
    {
        public IAsyncEnumerable<ProjectionTarget> Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithMultipleResultAsyncAsyncEnumerableProjection
    {
        public Task<IAsyncEnumerable<ProjectionTarget>> ProjectAsync(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithEnumerableResultExplicitNoMultipleResultProjection
    {
        [Projection(MultipleResults = MultipleProjectionResults.SingleResult)]
        public IEnumerable<ProjectionTarget> Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    [Projection(MultipleResults = MultipleProjectionResults.SingleResult)]
    public sealed class WithEnumerableResultExplicitClassNoMultipleResultProjection
    {
        public IEnumerable<ProjectionTarget> Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithInvalidExplicitMultipleResultProjection
    {
        [Projection(MultipleResults = MultipleProjectionResults.MultipleResults)]
        public ProjectionTarget Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithExplicitMultipleResultProjection
    {
        public IEnumerable<ProjectionTarget> Project(ProjectionSource x, int y)
        {
            throw null;
        }
    }

    public sealed class WithExplicitMultipleResultAsyncProjection
    {
        public Task<IEnumerable<ProjectionTarget>> ProjectAsync(ProjectionSource x, int y)
        {
            throw null;
        }
    }

#pragma warning restore IDE0060
}
