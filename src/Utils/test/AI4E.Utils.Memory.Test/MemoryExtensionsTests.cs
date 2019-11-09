using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Memory
{
    [TestClass]
    public sealed class MemoryExtensionsTests
    {
        [TestMethod]
        public void EqualSequenceHashCodeBlittableTest()
        {
            var buffer1 = new BlittableStruct[1027];
            var buffer2 = new BlittableStruct[1027];

            for (var i = 0; i < 1027; i++)
            {
                var v = new BlittableStruct { Int = i << 7, Float = i * 12.345f };

                buffer1[i] = v;
                buffer2[i] = v;
            }

            var hashCode1 = buffer1.AsSpan().SequenceHashCode();
            var hashCode2 = buffer1.AsSpan().SequenceHashCode();

            Assert.IsTrue(hashCode1 == hashCode2);
        }

        [TestMethod]
        public void EqualSequenceHashCodeNonBlittableTest()
        {
            var buffer1 = new string[1027];
            var buffer2 = new string[1027];

            for (var i = 0; i < 1027; i++)
            {
                var v = (i << 7).ToString();

                buffer1[i] = v;
                buffer2[i] = v;
            }

            var hashCode1 = buffer1.AsSpan().SequenceHashCode();
            var hashCode2 = buffer1.AsSpan().SequenceHashCode();

            Assert.IsTrue(hashCode1 == hashCode2);
        }

        private struct BlittableStruct
        {
            public int Int;
            public float Float;
        }
    }
}
