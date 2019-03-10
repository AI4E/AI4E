using System.Linq;
using System.Threading.Tasks;
using AI4E.Coordination.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination
{
    [TestClass]
    public class CoordinationManagerTests
    {
        //[TestMethod]
        public async Task TestCreateRootNoContention()
        {
            var services = Setup.BuildDefaultInMemorySetup();
            var coordinationManager = services.GetRequiredService<ICoordinationManager>();

            var rootPath = new CoordinationEntryPath();
            var payload = new byte[] { 0, 1, 2, 3 };

            var entry = await coordinationManager.CreateAsync(rootPath, payload, EntryCreationModes.Default, cancellation: default);

            Assert.IsNotNull(entry);
            Assert.AreSame(coordinationManager, entry.CoordinationManager);
            Assert.AreEqual(rootPath, entry.Path);
            Assert.AreEqual(rootPath, entry.ParentPath);
            Assert.AreEqual(1, entry.Version);
            Assert.IsTrue(payload.SequenceEqual(entry.Value.ToArray()));
        }
    }
}
