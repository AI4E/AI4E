using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AI4E.Storage.Domain.Projection
{
    [TestClass]
    public sealed class ProjectionFeatureProviderTests
    {
        [TestMethod]
        public void PublicClassWithoutSuffixTest()
        {
            Assert.IsFalse(ProjectionResolver.IsProjection(typeof(PublicClassWithoutSuffix)));
        }

        [TestMethod]
        public void PublicClassWithSuffixTest()
        {
            Assert.IsTrue(ProjectionResolver.IsProjection(typeof(PublicClassWithSuffixProjection)));
        }

        [TestMethod]
        public void PublicClassWithAttributeTest()
        {
            Assert.IsTrue(ProjectionResolver.IsProjection(typeof(PublicClassWithAttribute)));
        }

        [TestMethod]
        public void GenericPublicClassTest()
        {
            Assert.IsFalse(ProjectionResolver.IsProjection(typeof(GenericPublicClassProjection<>)));
        }

        [TestMethod]
        public void AbstractPublicClassTest()
        {
            Assert.IsFalse(ProjectionResolver.IsProjection(typeof(AbstractPublicClassProjection)));
        }

        [TestMethod]
        public void PublicClassWithNoProjectionAttributeTest()
        {
            Assert.IsFalse(ProjectionResolver.IsProjection(typeof(PublicClassWithNoProjectionAttributeProjection)));
        }

        [TestMethod]
        public void InternalClassWithSuffixTest()
        {
            Assert.IsFalse(ProjectionResolver.IsProjection(typeof(InternalClassWithSuffixProjection)));
        }

        [TestMethod]
        public void InternalClassWithAttributeTest()
        {
            Assert.IsTrue(ProjectionResolver.IsProjection(typeof(InternalClassWithAttribute)));
        }

        public IEnumerable<Assembly> Assemblies
        {
            get
            {
                yield return typeof(PublicClassWithoutSuffix).Assembly;
                yield return typeof(PublicClassWithSuffixProjection).Assembly;
                yield return typeof(PublicClassWithAttribute).Assembly;
                yield return typeof(GenericPublicClassProjection<>).Assembly;
                yield return typeof(AbstractPublicClassProjection).Assembly;
                yield return typeof(PublicClassWithNoProjectionAttributeProjection).Assembly;
                yield return typeof(InternalClassWithSuffixProjection).Assembly;
                yield return typeof(InternalClassWithAttribute).Assembly;
                yield return typeof(PublicClassWithoutSuffix).Assembly;
                yield return typeof(PublicClassWithSuffixProjection).Assembly;
                yield return typeof(PublicClassWithAttribute).Assembly;
                yield return typeof(GenericPublicClassProjection<>).Assembly;
                yield return typeof(AbstractPublicClassProjection).Assembly;
                yield return typeof(PublicClassWithNoProjectionAttributeProjection).Assembly;
                yield return typeof(InternalClassWithSuffixProjection).Assembly;
                yield return typeof(InternalClassWithAttribute).Assembly;
            }
        }

        [TestMethod]
        public void PopulateFeaturesTest()
        {
            var assemblies = Assemblies;
            var assemblySourceMock = new Mock<IAssemblySource>();
            assemblySourceMock.Setup(assemblySource => assemblySource.Assemblies).Returns(Assemblies.ToList());
            assemblySourceMock.Setup(assemblySource => assemblySource.GetAssemblyLoadContext(It.IsAny<Assembly>()))
                .Returns(AssemblyLoadContext.Default);

            IProjectionResolver projectionResolver = new ProjectionResolver(
                Options.Create(new DomainProjectionOptions()));


            var projectionTypes = projectionResolver.ResolveProjections(assemblySourceMock.Object);

            Assert.IsTrue(projectionTypes.Contains(typeof(PublicClassWithSuffixProjection)));
            Assert.IsTrue(projectionTypes.Contains(typeof(PublicClassWithAttribute)));
            Assert.IsTrue(projectionTypes.Contains(typeof(InternalClassWithAttribute)));

            Assert.IsFalse(projectionTypes.Contains(typeof(PublicClassWithoutSuffix)));
            Assert.IsFalse(projectionTypes.Contains(typeof(GenericPublicClassProjection<>)));
            Assert.IsFalse(projectionTypes.Contains(typeof(AbstractPublicClassProjection)));
            Assert.IsFalse(projectionTypes.Contains(typeof(PublicClassWithNoProjectionAttributeProjection)));
            Assert.IsFalse(projectionTypes.Contains(typeof(InternalClassWithSuffixProjection)));
        }
    }

    public class PublicClassWithoutSuffix { }

    public class PublicClassWithSuffixProjection { }

    [Projection]
    public class PublicClassWithAttribute { }

    [Projection]
    public class GenericPublicClassProjection<T> { }

    [Projection]
    public abstract class AbstractPublicClassProjection { }

    [NoProjection]
    [Projection]
    public class PublicClassWithNoProjectionAttributeProjection { }

    internal class InternalClassWithSuffixProjection { }

    [Projection]
    internal class InternalClassWithAttribute { }
}
