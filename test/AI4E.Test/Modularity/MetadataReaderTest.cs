using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Modularity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Modularity
{
    [TestClass]
    public sealed class MetadataReaderTest
    {
        private IMetadataReader BuildMetadataReader()
        {
            return new MetadataReader();
        }

        [TestMethod]
        public async Task ReadMetadataTest()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("AI4E.Test.Modularity.metadata.json");

            Assert.IsNotNull(stream);

            var metadataReader = BuildMetadataReader();

            var metadata = await metadataReader.ReadMetadataAsync(stream, cancellation: default);

            Assert.AreEqual(new ModuleIdentifier("Test.Module"), metadata.Module);
            Assert.AreEqual(new ModuleVersion(1, 0, 1, true), metadata.Version);
            Assert.AreEqual("User descriptive name", metadata.Name);
            Assert.AreEqual("Author of the module", metadata.Author);
            Assert.AreEqual("User description", metadata.Description);

            // TODO: Release date

            Assert.AreEqual("dotnet MyApp.dll", metadata.EntryAssemblyCommand);
            Assert.AreEqual("%insert-argument%", metadata.EntryAssemblyArguments);

            Assert.AreEqual(2, metadata.Dependencies.Count());

            var firstDependency = metadata.Dependencies.First();

            Assert.AreEqual(new ModuleIdentifier("Test.Base.Module"), firstDependency.Id);
            Assert.AreEqual(new ModuleVersionFilter(1, 0, 0), firstDependency.Version);

            var secondDependency = metadata.Dependencies.Skip(1).First();

            Assert.AreEqual(new ModuleIdentifier("Test.Base.Module2"), secondDependency.Id);
            Assert.AreEqual(new ModuleVersionFilter(), secondDependency.Version);
        }
    }
}
