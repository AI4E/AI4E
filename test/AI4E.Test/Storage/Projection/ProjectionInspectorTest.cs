using System.Linq;
using System.Threading.Tasks;
using AI4E.Storage.Projection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Storage.Projection
{
    [TestClass]
    public sealed class ProjectionInspectorTest
    {
        [TestMethod]
        public void PocoProjectionTest()
        {
            var projectionInspector = new ProjectionInspector(typeof(PocoProjection));
            var projectionDescriptors = projectionInspector.GetDescriptors();

            Assert.AreEqual(6, projectionDescriptors.Count());

            var first = projectionDescriptors.First();

            Assert.AreEqual(typeof(ProjectionSource1), first.SourceType);
            Assert.IsFalse(first.MultipleResults);
            Assert.AreEqual(typeof(ProjectionResult<string>), first.ProjectionType);
            Assert.AreEqual(typeof(PocoProjection).GetMethod("Project"), first.Member);

            var second = projectionDescriptors.Skip(1).First();

            Assert.AreEqual(typeof(ProjectionSource1), second.SourceType);
            Assert.IsFalse(second.MultipleResults);
            Assert.AreEqual(typeof(ProjectionResult<string>), second.ProjectionType);
            Assert.AreEqual(typeof(PocoProjection).GetMethod("ProjectAsync"), second.Member);

            var third = projectionDescriptors.Skip(2).First();

            Assert.AreEqual(typeof(ProjectionSource2), third.SourceType);
            Assert.IsFalse(third.MultipleResults);
            Assert.AreEqual(typeof(ProjectionResult<string>), third.ProjectionType);
            Assert.AreEqual(typeof(PocoProjection).GetMethod("Project2"), third.Member);

            var fourth = projectionDescriptors.Skip(3).First();

            Assert.AreEqual(typeof(ProjectionSource2), fourth.SourceType);
            Assert.IsFalse(fourth.MultipleResults);
            Assert.AreEqual(typeof(ProjectionResult<string>), fourth.ProjectionType);
            Assert.AreEqual(typeof(PocoProjection).GetMethod("ProjectToXX"), fourth.Member);

            var fith = projectionDescriptors.Skip(4).First();

            Assert.AreEqual(typeof(ProjectionSource2), fith.SourceType);
            Assert.IsFalse(fith.MultipleResults);
            Assert.AreEqual(typeof(ProjectionResult<string>), fith.ProjectionType);
            Assert.AreEqual(typeof(PocoProjection).GetMethod("ABC"), fith.Member);

            var sixth = projectionDescriptors.Skip(5).First();

            Assert.AreEqual(typeof(ProjectionSource1), sixth.SourceType);
            Assert.IsFalse(sixth.MultipleResults);
            Assert.AreEqual(typeof(object), sixth.ProjectionType);
            Assert.AreEqual(typeof(PocoProjection).GetMethod("ABC2"), sixth.Member);
        }
    }

    public sealed class PocoProjection
    {
        public ProjectionResult<string> Project(ProjectionSource1 projectionSource)
        {
            return new ProjectionResult<string>(projectionSource.String);
        }

        public Task<ProjectionResult<string>> ProjectAsync(ProjectionSource1 projectionSource)
        {
            return Task.FromResult(new ProjectionResult<string>(projectionSource.String));
        }

        public Task<ProjectionResult<string>> Project2(ProjectionSource2 projectionSource)
        {
            return Task.FromResult(new ProjectionResult<string>(projectionSource.String));
        }

        public Task<ProjectionResult<string>> ProjectToXX(ProjectionSource2 projectionSource)
        {
            return Task.FromResult(new ProjectionResult<string>(projectionSource.String));
        }

        // Async suffix for non-async method
        public ProjectionResult<string> Project2Async(ProjectionSource2 projectionSource)
        {
            return new ProjectionResult<string>(projectionSource.String);
        }

        // Project-prefix missing
        public ProjectionResult<string> ArbitraryNamed(ProjectionSource1 projectionSource)
        {
            return new ProjectionResult<string>(projectionSource.String);
        }

        [NoProjectionMember]
        public Task<ProjectionResult<string>> ProjectToXY(ProjectionSource2 projectionSource)
        {
            return Task.FromResult(new ProjectionResult<string>(projectionSource.String));
        }

        [ProjectionMember]
        public Task<ProjectionResult<string>> ABC(ProjectionSource2 projectionSource)
        {
            return Task.FromResult(new ProjectionResult<string>(projectionSource.String));
        }

        [ProjectionMember(ProjectionType = typeof(object), SourceType = typeof(ProjectionSource1))]
        public Task<ProjectionResult<string>> ABC2(object projectionSource)
        {
            return Task.FromResult(new ProjectionResult<string>((projectionSource as ProjectionSource1).String));
        }
    }

    public sealed class ProjectionResult<T>
    {
        public ProjectionResult(T result)
        {
            Result = result;
        }

        public T Result { get; }
    }

    public sealed class ProjectionSource1
    {
        public string String { get; set; }
        public int Int { get; set; }
        public double Double { get; set; }
    }

    public sealed class ProjectionSource2
    {
        public string String { get; set; }
        public int Int { get; set; }
        public double Double { get; set; }
    }
}
