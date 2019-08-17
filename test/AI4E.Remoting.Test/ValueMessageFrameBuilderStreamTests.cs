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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting
{
    [TestClass]
    public sealed class ValueMessageFrameBuilderStreamTests
    {
        [TestMethod]
        public void CreateThrowsOnNullBuilder()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                new ValueMessageFrameBuilderStream(null, default);
            });
        }

        [TestMethod]
        public void ReadTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            var buffer = new byte[45];

            var bytesRead = subject.Read(buffer, 0, 45);

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer));
        }

        [TestMethod]
        public void ReadBeyondEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            var buffer = new byte[80];

            var bytesRead = subject.Read(buffer, 0, 80);

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer.Take(45)));
        }

        [TestMethod]
        public void ReadEndedStreamTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            var buffer = new byte[1];

            var bytesRead = subject.Read(buffer, 0, 1);

            Assert.AreEqual(0, bytesRead);
        }

        [TestMethod]
        public void ReadTouchedTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            var buffer = new byte[45];

            var bytesRead = subject.Read(buffer, 0, 45);

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer));
        }

        [TestMethod]
        public void ReadTouchedBeyondEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            var buffer = new byte[80];

            var bytesRead = subject.Read(buffer, 0, 80);

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer.Take(45)));
        }

        [TestMethod]
        public void ReadTouchedEndedStreamTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            var buffer = new byte[1];

            var bytesRead = subject.Read(buffer, 0, 1);

            Assert.AreEqual(0, bytesRead);
        }

        [TestMethod]
        public void ReadThrowsOnNullBufferTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                subject.Read(null, 0, 1);
            });
        }

        [TestMethod]
        public void ReadThrowsOnNegativeOffsetTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.Read(new byte[1], -1, 1);
            });
        }

        [TestMethod]
        public void ReadThrowsOnInsufficientSpaceTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentException>(() =>
            {
                subject.Read(new byte[1], 0, 2);
            });
        }

        [TestMethod]
        public void ReadSpanTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            var buffer = new byte[45];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 45));

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer));
        }

        [TestMethod]
        public void ReadSpanBeyondEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            var buffer = new byte[80];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 80));

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer.Take(45)));
        }

        [TestMethod]
        public void ReadSpanEndedStreamTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            var buffer = new byte[1];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 1));

            Assert.AreEqual(0, bytesRead);
        }

        [TestMethod]
        public void ReadTouchedSpanTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            var buffer = new byte[45];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 45));

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer));
        }

        [TestMethod]
        public void ReadTouchedSpanBeyondEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            var buffer = new byte[80];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 80));

            Assert.AreEqual(45, bytesRead);
            Assert.IsTrue(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).SequenceEqual(buffer.Take(45)));
        }

        [TestMethod]
        public void ReadTouchedSpanEndedStreamTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            var buffer = new byte[1];

            var bytesRead = subject.Read(buffer.AsSpan().Slice(0, 1));

            Assert.AreEqual(0, bytesRead);
        }

        [TestMethod]
        public void CanReadTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            Assert.IsTrue(subject.CanRead);
        }

        [TestMethod]
        public void CanWriteTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            Assert.IsTrue(subject.CanWrite);
        }

        [TestMethod]
        public void CanSeekTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            Assert.IsTrue(subject.CanSeek);
        }

        [TestMethod]
        public void LengthTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            Assert.AreEqual(45, subject.Length);
        }

        [TestMethod]
        public void EmptyStreamPositionTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void GetPositionTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Read(new byte[10], 0, 10);

            Assert.AreEqual(10, subject.Position);
        }

        [TestMethod]
        public void SetPositionTest()
        {
            var frame = BuildFrameBuilder();
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
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.Position = -1;
            });
        }

        [TestMethod]
        public void SetPositionThrowsOnValueLargerLengthTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.Position = subject.Length + 1;
            });
        }

        [TestMethod]
        public void SeekBeginTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(0, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekFromBeginTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(10, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(10, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromBeginTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-10, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekFromBeginBeyondEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(100, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(100, subject.Length);
            Assert.AreEqual(100, subject.Position);
        }

        [TestMethod]
        public void SeekCurrentTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(0, System.IO.SeekOrigin.Current);

            Assert.AreEqual(20, subject.Position);
        }

        [TestMethod]
        public void SeekFromCurrentTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(10, System.IO.SeekOrigin.Current);

            Assert.AreEqual(30, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromCurrentTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-10, System.IO.SeekOrigin.Current);

            Assert.AreEqual(10, subject.Position);
        }

        [TestMethod]
        public void SeekFromCurrentBeyondEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(100, System.IO.SeekOrigin.Current);

            Assert.AreEqual(120, subject.Length);
            Assert.AreEqual(120, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromCurrentBeyondBeginTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-100, System.IO.SeekOrigin.Current);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(0, System.IO.SeekOrigin.End);

            Assert.AreEqual(subject.Length, subject.Position);
        }

        [TestMethod]
        public void SeekFromEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(10, System.IO.SeekOrigin.End);

            Assert.AreEqual(subject.Length, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromEndTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-10, System.IO.SeekOrigin.End);

            Assert.AreEqual(35, subject.Position);
        }

        [TestMethod]
        public void SeekNegativeFromEndBeyondBeginTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            subject.Position = 20;
            subject.Seek(-100, System.IO.SeekOrigin.End);

            Assert.AreEqual(0, subject.Position);
        }

        [TestMethod]
        public void SeekThrowsOnInvalidOriginTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentException>(() =>
            {
                subject.Seek(-0, (System.IO.SeekOrigin)100);
            });
        }

        [TestMethod]
        public void SetLengthTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.SetLength(10);

            Assert.AreEqual(10, subject.Length);
        }

        [TestMethod]
        public void SetLengthEnlargeTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.SetLength(100);

            Assert.AreEqual(100, subject.Length);

            var buffer1 = new byte[45];
            subject.Read(buffer1, 0, 45);

            var buffer2 = new byte[100 - 45];
            var bytesRead = subject.Read(buffer2, 0, buffer2.Length);

            Assert.IsTrue(buffer1.SequenceEqual(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray()));
            Assert.AreEqual(buffer2.Length, bytesRead);
            Assert.IsTrue(buffer2.All(p => p == 0));
        }

        [TestMethod]
        public void SetLengthTouchedTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            subject.SetLength(10);

            Assert.AreEqual(10, subject.Length);
        }

        [TestMethod]
        public void SetLengthEnlargeTouchedTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();
            subject.Touch(0);
            subject.SetLength(100);

            Assert.AreEqual(100, subject.Length);

            var buffer1 = new byte[45];
            subject.Read(buffer1, 0, 45);

            var buffer2 = new byte[100 - 45];
            var bytesRead = subject.Read(buffer2, 0, buffer2.Length);

            Assert.IsTrue(buffer1.SequenceEqual(Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray()));
            Assert.AreEqual(buffer2.Length, bytesRead);
            Assert.IsTrue(buffer2.All(p => p == 0));
        }

        [TestMethod]
        public void SetLengthThrowsOnNegativeValueTest()
        {
            var frame = BuildFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.SetLength(-1);
            });
        }

        [TestMethod]
        public void WriteTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();

            subject.Write(payload, 0, payload.Length);

            Assert.AreEqual(payload.Length, subject.Position);
            Assert.AreEqual(payload.Length, subject.Length);

            subject.Position = 0;
            var buffer = new byte[payload.Length];
            var bytesRead = subject.Read(buffer, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer));
        }

        [TestMethod]
        public void WriteTouchedTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();
            subject.Touch(0);
            subject.Write(payload, 0, payload.Length);

            Assert.AreEqual(payload.Length, subject.Position);
            Assert.AreEqual(payload.Length, subject.Length);

            subject.Position = 0;
            var buffer = new byte[payload.Length];
            var bytesRead = subject.Read(buffer, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer));
        }

        [TestMethod]
        public void WriteAtPositionTest()
        {
            var existingPayload = new byte[] { 1, 2, 3, 4, 5, 6 };

            var frame = new ValueMessageFrameBuilder();
            frame.UnsafeReplacePayloadWithoutCopy(existingPayload);
            var subject = frame.OpenStream();
            subject.Position = 3;
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();

            subject.Write(payload, 0, payload.Length);

            Assert.AreEqual(payload.Length + 3, subject.Position);
            Assert.AreEqual(payload.Length + 3, subject.Length);

            subject.Position = 3;
            var buffer1 = new byte[payload.Length];
            var bytesRead = subject.Read(buffer1, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer1));

            subject.Position = 0;

            var buffer2 = new byte[3];
            subject.Read(buffer2, 0, 3);

            Assert.IsTrue(new byte[] { 1, 2, 3 }.SequenceEqual(buffer2));
        }

        [TestMethod]
        public void WriteTouchedAtPositionTest()
        {
            var existingPayload = new byte[] { 1, 2, 3, 4, 5, 6 };

            var frame = new ValueMessageFrameBuilder();
            frame.UnsafeReplacePayloadWithoutCopy(existingPayload);
            var subject = frame.OpenStream();
            subject.Position = 3;
            subject.Touch(0);
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();

            subject.Write(payload, 0, payload.Length);

            Assert.AreEqual(payload.Length + 3, subject.Position);
            Assert.AreEqual(payload.Length + 3, subject.Length);

            subject.Position = 3;
            var buffer1 = new byte[payload.Length];
            var bytesRead = subject.Read(buffer1, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer1));

            subject.Position = 0;

            var buffer2 = new byte[3];
            subject.Read(buffer2, 0, 3);

            Assert.IsTrue(new byte[] { 1, 2, 3 }.SequenceEqual(buffer2));
        }


        [TestMethod]
        public void WriteSpanTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();

            subject.Write(payload.AsSpan());

            Assert.AreEqual(payload.Length, subject.Position);
            Assert.AreEqual(payload.Length, subject.Length);

            subject.Position = 0;
            var buffer = new byte[payload.Length];
            var bytesRead = subject.Read(buffer, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer));
        }

        [TestMethod]
        public void WriteTouchedSpanTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();
            subject.Touch(0);
            subject.Write(payload.AsSpan());

            Assert.AreEqual(payload.Length, subject.Position);
            Assert.AreEqual(payload.Length, subject.Length);

            subject.Position = 0;
            var buffer = new byte[payload.Length];
            var bytesRead = subject.Read(buffer, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer));
        }

        [TestMethod]
        public void WriteSpanAtPositionTest()
        {
            var existingPayload = new byte[] { 1, 2, 3, 4, 5, 6 };

            var frame = new ValueMessageFrameBuilder();
            frame.UnsafeReplacePayloadWithoutCopy(existingPayload);
            var subject = frame.OpenStream();
            subject.Position = 3;
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();

            subject.Write(payload.AsSpan());

            Assert.AreEqual(payload.Length + 3, subject.Position);
            Assert.AreEqual(payload.Length + 3, subject.Length);

            subject.Position = 3;
            var buffer1 = new byte[payload.Length];
            var bytesRead = subject.Read(buffer1, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer1));

            subject.Position = 0;

            var buffer2 = new byte[3];
            subject.Read(buffer2, 0, 3);

            Assert.IsTrue(new byte[] { 1, 2, 3 }.SequenceEqual(buffer2));
        }

        [TestMethod]
        public void WriteTouchedSpanAtPositionTest()
        {
            var existingPayload = new byte[] { 1, 2, 3, 4, 5, 6 };

            var frame = new ValueMessageFrameBuilder();
            frame.UnsafeReplacePayloadWithoutCopy(existingPayload);
            var subject = frame.OpenStream();
            subject.Position = 3;
            subject.Touch(0);
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();

            subject.Write(payload.AsSpan());

            Assert.AreEqual(payload.Length + 3, subject.Position);
            Assert.AreEqual(payload.Length + 3, subject.Length);

            subject.Position = 3;
            var buffer1 = new byte[payload.Length];
            var bytesRead = subject.Read(buffer1, 0, payload.Length);

            Assert.AreEqual(payload.Length, bytesRead);
            Assert.IsTrue(payload.SequenceEqual(buffer1));

            subject.Position = 0;

            var buffer2 = new byte[3];
            subject.Read(buffer2, 0, 3);

            Assert.IsTrue(new byte[] { 1, 2, 3 }.SequenceEqual(buffer2));
        }


        [TestMethod]
        public void WriteThrowsOnNullBufferTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                subject.Write(null, 0, 1);
            });
        }

        [TestMethod]
        public void WriteThrowsOnNegativeOffsetTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                subject.Write(new byte[1], -1, 1);
            });
        }

        [TestMethod]
        public void WriteThrowsOnInsufficientSpaceTest()
        {
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            Assert.ThrowsException<ArgumentException>(() =>
            {
                subject.Write(new byte[1], 0, 2);
            });
        }

        [TestMethod]
        public void FlushTest()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5, 6 };
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            subject.Write(payload, 0, payload.Length);

            subject.Flush();

            Assert.IsTrue(payload.SequenceEqual(frame.Payload.ToArray()));
        }

        [TestMethod]
        public void WriteAfterFlushTest()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5, 6 };
            var payload2 = new byte[] { 0, 0, 0, 0, 0, 0 };
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            subject.Write(payload, 0, payload.Length);

            subject.Flush();

            subject.Position = 0;
            subject.Write(payload2);

            Assert.IsTrue(payload.SequenceEqual(frame.Payload.ToArray()));
        }

        [TestMethod]
        public void DisposeTest()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5, 6 };
            var frame = new ValueMessageFrameBuilder();
            var subject = frame.OpenStream();

            subject.Write(payload, 0, payload.Length);

            subject.Dispose();

            Assert.IsTrue(payload.SequenceEqual(frame.Payload.ToArray()));
        }

        private static ValueMessageFrameBuilder BuildFrameBuilder()
        {
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();
            var result = new ValueMessageFrameBuilder();
            result.UnsafeReplacePayloadWithoutCopy(payload);
            return result;
        }
    }
}
