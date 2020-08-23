/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public sealed class MessageFrameStreamTests
    {
        [TestMethod]
        public void ReadTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            var buffer = new byte[45];

            var bytesRead = subject.Read(buffer, 0, 45);

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer));
        }

        [TestMethod]
        public void ReadBeyondEndTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            var buffer = new byte[80];

            var bytesRead = subject.Read(buffer, 0, 80);

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer.Take(45)));
        }

        [TestMethod]
        public void ReadEndedStreamTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();

            var buffer = new byte[1];

            var bytesRead = subject.Read(buffer, 0, 1);

            Assert.AreEqual(0, bytesRead);
        }

        [TestMethod]
        public void ReadThrowsOnNullBufferTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                subject.Read(null, 0, 1);
            });
        }

        [TestMethod]
        public void ReadThrowsOnNegativeOffsetTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.Read(new byte[1], -1, 1);
            });
        }

        [TestMethod]
        public void ReadThrowsOnInsufficientSpaceTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentException>(() =>
            {
                subject.Read(new byte[1], 0, 2);
            });
        }

        [TestMethod]
        public void ReadSpanTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            var buffer = new byte[45];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 45));

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer));
        }

        [TestMethod]
        public void ReadSpanBeyondEndTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            var buffer = new byte[80];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 80));

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer.Take(45)));
        }

        [TestMethod]
        public void ReadSpanEndedStreamTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();

            var buffer = new byte[1];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 1));

            Assert.AreEqual(0, bytesRead);
        }

        [TestMethod]
        public void SetLengthThrowsTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();

            Assert.ThrowsException<NotSupportedException>(() =>
            {
                subject.SetLength(123);
            });
        }

        [TestMethod]
        public void WriteThrowsTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();

            Assert.ThrowsException<NotSupportedException>(() =>
            {
                subject.Write(new byte[10], 0, 10);
            });
        }

        [TestMethod]
        public void CanReadTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();
            Assert.IsTrue(subject.CanRead);
        }

        [TestMethod]
        public void CanWriteTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();
            Assert.IsFalse(subject.CanWrite);
        }

        [TestMethod]
        public void CanSeekTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();
            Assert.IsTrue(subject.CanSeek);
        }

        [TestMethod]
        public void LengthTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();
            Assert.AreEqual(45, subject.Length);
        }

        [TestMethod]
        public void EmptyStreamPositionTest()
        {
            var frame = default(MessageFrame);
            var subject = frame.OpenStream();
            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void GetPositionTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Read(new byte[10], 0, 10);

            Assert.AreEqual(10, subject.Position);
        }

        [TestMethod]
        public void SetPositionTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 10;

            Assert.AreEqual(10, subject.Position);

            var buffer = new byte[10];
            subject.Read(buffer, 0, 10);

            Assert.IsTrue(Enumerable.Range(10, 10).Select(p => unchecked((byte)p)).SequenceEqual(buffer));
        }

        [TestMethod]
        public void SetPositionThrowsOnNegativeValueTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.Position = -1;
            });
        }

        [TestMethod]
        public void SetPositionThrowsOnValueLargerLengthTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.Position = subject.Length + 1;
            });
        }

        [TestMethod]
        public void SeekBeginTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(0, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekFromBeginTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(10, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(10, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromBeginTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-10, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekFromBeginBeyondEndTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(100, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(45, subject.Length);
            Assert.AreEqual(45, subject.Position);
        }

        [TestMethod]
        public void SeekCurrentTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(0, System.IO.SeekOrigin.Current);

            Assert.AreEqual(20, subject.Position);
        }

        [TestMethod]
        public void SeekFromCurrentTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(10, System.IO.SeekOrigin.Current);

            Assert.AreEqual(30, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromCurrentTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-10, System.IO.SeekOrigin.Current);

            Assert.AreEqual(10, subject.Position);
        }

        [TestMethod]
        public void SeekFromCurrentBeyondEndTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(100, System.IO.SeekOrigin.Current);

            Assert.AreEqual(45, subject.Length);
            Assert.AreEqual(45, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromCurrentBeyondBeginTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-100, System.IO.SeekOrigin.Current);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekEndTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(0, System.IO.SeekOrigin.End);

            Assert.AreEqual(subject.Length, subject.Position);
        }

        [TestMethod]
        public void SeekFromEndTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(10, System.IO.SeekOrigin.End);

            Assert.AreEqual(subject.Length, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromEndTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-10, System.IO.SeekOrigin.End);

            Assert.AreEqual(35, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromEndBeyondBeginTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-100, System.IO.SeekOrigin.End);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekThrowsOnInvalidOriginTest()
        {
            var frame = BuildFrame();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentException>(() =>
            {
                subject.Seek(-0, (System.IO.SeekOrigin)100);
            });
        }

        private static MessageFrame BuildFrame()
        {
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();
            return new MessageFrame(payload);
        }
    }
}
