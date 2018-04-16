using AI4E.Coordination;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Coordination
{
    [TestClass]
    public class EntryPathHelperTest
    {
        [TestMethod]
        public void NormalizePathTest()
        {
            var path = "";
            var normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/", normalizedPath);

            path = "       ";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/", normalizedPath);

            path = "/";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/", normalizedPath);

            path = " \t\t  /    \t ";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/", normalizedPath);

            path = " \t\t\\             \t   ";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/", normalizedPath);

            path = "  /x    \\\t    y    ";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/x/y", normalizedPath);

            path = "  x    \\\t    y    ";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/x/y", normalizedPath);

            path = " / x    \\\t    y   \\ ";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/x/y", normalizedPath);

            path = " x    \\\t    y   \\ ";
            normalizedPath = EntryPathHelper.NormalizePath(path);
            Assert.AreEqual("/x/y", normalizedPath);
        }

        [TestMethod]
        public void GetParentPathTest()
        {
            var path = " x    \\\t    y   \\ z \t\t////////////     ";
            var parentPath = EntryPathHelper.GetParentPath(path, out var name, normalize: true);
            Assert.AreEqual("/x/y", parentPath);
            Assert.AreEqual("z", name);

            path = "/";
            parentPath = EntryPathHelper.GetParentPath(path, out name, normalize: true);
            Assert.IsNull(parentPath);
            Assert.AreEqual("", name);

        }

        [TestMethod]
        public void GetChildPathTest()
        {
            var path = " x    \\\t    y   \\ \t\t////////////     ";
            var child = "\\ z \t\t////////////     ";
            var childPath = EntryPathHelper.GetChildPath(path, child, normalize: true);
            Assert.AreEqual("/x/y/z", childPath);

            path = " x    \\\t    y";
            child = "\\ z \t\t////////////     ";
            childPath = EntryPathHelper.GetChildPath(path, child, normalize: true);
            Assert.AreEqual("/x/y/z", childPath);

            path = "      ";
            child = "\\ z \t\t////////////     ";
            childPath = EntryPathHelper.GetChildPath(path, child, normalize: true);
            Assert.AreEqual("/z", childPath);

            path = "";
            child = "\\ z \t\t////////////     ";
            childPath = EntryPathHelper.GetChildPath(path, child, normalize: true);
            Assert.AreEqual("/z", childPath);
        }
    }
}
