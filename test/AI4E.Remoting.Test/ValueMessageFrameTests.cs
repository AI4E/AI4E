/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Linq;
using AI4E.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting
{
    [TestClass]
    public sealed class ValueMessageFrameTests
    {
        [TestMethod]
        public void DefaultValueMessageFrameTest()
        {
            var subject = default(ValueMessageFrame);

            Assert.AreEqual(1, subject.Length);
            Assert.AreEqual(0, subject.Payload.Length);
        }

        [TestMethod]
        public void DefaultValueMessageFrameWriteTest()
        {
            var subject = default(ValueMessageFrame);
            var buffer = new byte[subject.Length];

            ValueMessageFrame.Write(subject, buffer);

            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(0, buffer[0]); // The frame length is header-exclusive.
        }

        [TestMethod]
        public void CreateValueMessageFrameTest()
        {
            var payload = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();
            var subject = new ValueMessageFrame(payload.AsSpan());

            Assert.AreEqual(payload.Length + 2, subject.Length);
            Assert.IsTrue(payload.SequenceEqual(subject.Payload.ToArray()));
        }

        [TestMethod]
        public void CreateValueMessageFrameCopyTest()
        {
            var payload = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();
            var subject = new ValueMessageFrame(payload.AsMemory(), createCopy: true);

            // We modify the original payload to assert that the frame performs a copy internally
            Array.Clear(payload, 0, payload.Length);

            Assert.AreEqual(payload.Length + 2, subject.Length);
            Assert.IsTrue(subject.Payload.ToArray().SequenceEqual(Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray()));
        }

        [TestMethod]
        public void CreateValueMessageFrameNoCopyTest()
        {
            var payload = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();
            var subject = ValueMessageFrame.UnsafeCreateWithoutCopy(payload.AsMemory());

            // We modify the original payload to assert that the frame does NOT perform a copy internally
            Array.Clear(payload, 0, payload.Length);

            Assert.AreEqual(payload.Length + 2, subject.Length);
            Assert.IsTrue(payload.SequenceEqual(subject.Payload.ToArray()));
        }

        [TestMethod]
        public void ReadTest()
        {
            var data = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();

            LengthCodeHelper.Write7BitEncodedInt(data.AsSpan(), data.Length - 2); // The frame length is header-exclusive.

            var subject = ValueMessageFrame.Read(data.AsSpan());

            Assert.AreEqual(data.Length, subject.Length);
            Assert.IsTrue(data[2..].SequenceEqual(subject.Payload.ToArray()));
        }

        [TestMethod]
        public void ReadCopyTest()
        {
            var data = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();

            LengthCodeHelper.Write7BitEncodedInt(data.AsSpan(), data.Length - 2); // The frame length is header-exclusive.

            var subject = ValueMessageFrame.Read(data.AsMemory(), createCopy: true);

            // We modify the original payload to assert that the frame performs a copy internally
            Array.Clear(data, 0, data.Length);

            Assert.AreEqual(data.Length, subject.Length);
            Assert.IsTrue(Enumerable.Range(2, 0x82).Select(p => unchecked((byte)p)).ToArray().SequenceEqual(subject.Payload.ToArray()));
        }

        [TestMethod]
        public void ReadNoCopyTest()
        {
            var data = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();

            LengthCodeHelper.Write7BitEncodedInt(data.AsSpan(), data.Length - 2); // The frame length is header-exclusive.

            var subject = ValueMessageFrame.Read(data.AsMemory(), createCopy: false);

            // We modify the original payload to assert that the frame does NOT perform a copy internally
            Array.Clear(data, 0, data.Length);

            Assert.AreEqual(data.Length, subject.Length);
            Assert.IsTrue(data[2..].SequenceEqual(subject.Payload.ToArray()));
        }

        [TestMethod]
        public void ReadWriteRountripTest()
        {
            var payload = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();
            var subject = new ValueMessageFrame(payload);
            var buffer = new byte[subject.Length];
            ValueMessageFrame.Write(subject, buffer);
            var subject2 = ValueMessageFrame.Read(buffer.AsSpan());

            Assert.AreEqual(payload.Length + LengthCodeHelper.Get7BitEndodedIntBytesCount(payload.Length), subject2.Length);
            Assert.IsTrue(payload.SequenceEqual(subject2.Payload.ToArray()));
        }
    }
}
