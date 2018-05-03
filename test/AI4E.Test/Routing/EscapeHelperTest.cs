using System.Text;
using AI4E.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Routing
{
    [TestClass]
    public sealed class EscapeHelperTest
    {
        [TestMethod]
        public void EscapeTest()
        {
            var input = "1--KO-N0ß/jh\\56->zkm30KJK-/";
            var expectedOutput = "1----KO--N0ß-Xjh-Y56-->zkm30KJK---X";

            var sb = new StringBuilder(input);
            EscapeHelper.Escape(sb, 0);

            Assert.AreEqual(expectedOutput, sb.ToString());

            expectedOutput = "1--KO-N0ß-Xjh-Y56-->zkm30KJK---X";

            sb = new StringBuilder(input);
            EscapeHelper.Escape(sb, 9);

            Assert.AreEqual(expectedOutput, sb.ToString());
        }

        [TestMethod]
        public void UnescapeTest()
        {
            var input = "1----KO--N0ß-Xjh-Y56-->zkm30KJK---X";
            var expectedOutput = "1--KO-N0ß/jh\\56->zkm30KJK-/";

            var sb = new StringBuilder(input);
            EscapeHelper.Unescape(sb, 0);

            Assert.AreEqual(expectedOutput, sb.ToString());

            expectedOutput = "1----KO--N0ß/jh\\56->zkm30KJK-/";

            sb = new StringBuilder(input);
            EscapeHelper.Unescape(sb, 12);

            Assert.AreEqual(expectedOutput, sb.ToString());
        }
    }
}
