using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AI4E.Utils.ApplicationParts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Domain.Projection
{
    [TestClass]
    public sealed class ProjectionFeatureProviderTests
    {
        [TestMethod]
        public void PublicClassWithoutSuffixTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsFalse(provider.IsProjection(typeof(PublicClassWithoutSuffix)));
        }

        [TestMethod]
        public void PublicClassWithSuffixTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsTrue(provider.IsProjection(typeof(PublicClassWithSuffixProjection)));
        }

        [TestMethod]
        public void PublicClassWithAttributeTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsTrue(provider.IsProjection(typeof(PublicClassWithAttribute)));
        }

        [TestMethod]
        public void GenericPublicClassTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsFalse(provider.IsProjection(typeof(GenericPublicClassProjection<>)));
        }

        [TestMethod]
        public void AbstractPublicClassTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsFalse(provider.IsProjection(typeof(AbstractPublicClassProjection)));
        }

        [TestMethod]
        public void PublicClassWithNoProjectionAttributeTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsFalse(provider.IsProjection(typeof(PublicClassWithNoProjectionAttributeProjection)));
        }

        [TestMethod]
        public void InternalClassWithSuffixTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsFalse(provider.IsProjection(typeof(InternalClassWithSuffixProjection)));
        }

        [TestMethod]
        public void InternalClassWithAttributeTest()
        {
            var provider = new ProjectionFeatureProvider();

            Assert.IsTrue(provider.IsProjection(typeof(InternalClassWithAttribute)));
        }

        [TestMethod]
        public void PopulateFeaturesTest()
        {
            var appParts = new[]
            {
                // We register this twice to test type deduplication
                new ApplicationPartTypeProviderMock(),
                new ApplicationPartTypeProviderMock()
            };

            var feature = new ProjectionFeature();
            var provider = new ProjectionFeatureProvider();

            provider.PopulateFeature(appParts, feature);

            Assert.AreEqual(3, feature.Projections.Count);
            Assert.IsTrue(new[]
            {
                typeof(PublicClassWithSuffixProjection),
                typeof(PublicClassWithAttribute),
                typeof(InternalClassWithAttribute)
            }.SequenceEqual(feature.Projections));
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

    public sealed class ApplicationPartTypeProviderMock : ApplicationPart, IApplicationPartTypeProvider
    {
        public IEnumerable<TypeInfo> Types
        {
            get
            {
                yield return typeof(PublicClassWithoutSuffix).GetTypeInfo();
                yield return typeof(PublicClassWithSuffixProjection).GetTypeInfo();
                yield return typeof(PublicClassWithAttribute).GetTypeInfo();
                yield return typeof(GenericPublicClassProjection<>).GetTypeInfo();
                yield return typeof(AbstractPublicClassProjection).GetTypeInfo();
                yield return typeof(PublicClassWithNoProjectionAttributeProjection).GetTypeInfo();
                yield return typeof(InternalClassWithSuffixProjection).GetTypeInfo();
                yield return typeof(InternalClassWithAttribute).GetTypeInfo();
            }
        }

        public override string Name => nameof(ApplicationPartTypeProviderMock);
    }
}
