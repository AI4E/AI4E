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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting
{
    [TestClass]
    public sealed class ValueMessageTests
    {
        [TestMethod]
        public void DefaultValueMessageTest()
        {
            var subject = default(ValueMessage);

            Assert.AreEqual(1, subject.Length);
            Assert.AreEqual(0, subject.Frames.Count);
        }

        [TestMethod]
        public void DefaultValueMessagePopFrameTest()
        {
            var subject = default(ValueMessage);

            subject = subject.PopFrame(out var frame);

            Assert.AreEqual(1, frame.Length);
            Assert.AreEqual(0, frame.Payload.Length);

            Assert.AreEqual(1, subject.Length);
            Assert.AreEqual(0, subject.Frames.Count);
        }

        [TestMethod]
        public void DefaultValueMessagePushFrameTest()
        {
            var subject = default(ValueMessage);

            var payload = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();
            var frame = new ValueMessageFrame(payload.AsSpan());

            subject = subject.PushFrame(frame);

            Assert.AreEqual(payload.Length + 4, subject.Length);
            Assert.AreEqual(1, subject.Frames.Count);
            Assert.AreEqual(payload.Length + 2, subject.Frames[0].Length);
            Assert.IsTrue(payload.SequenceEqual(subject.Frames[0].Payload.ToArray()));
        }

        [TestMethod]
        public void PushFrameTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 })
            };

            var subject = new ValueMessage(frames);

            var payload = Enumerable.Range(0, 0x84).Select(p => unchecked((byte)p)).ToArray();
            var frame = new ValueMessageFrame(payload.AsSpan());

            subject = subject.PushFrame(frame);

            Assert.AreEqual(3, subject.Frames.Count);

            Assert.AreEqual(payload.Length + 2, subject.Frames[2].Length);
            Assert.IsTrue(payload.SequenceEqual(subject.Frames[2].Payload.ToArray()));
        }

        [TestMethod]
        public void PopFrameTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessage(frames);
            subject = subject.PopFrame(out var frame);

            Assert.AreEqual(2, subject.Frames.Count);

            Assert.AreEqual(4, frame.Length);
            Assert.IsTrue(new byte[] { 3, 4, 5 }.SequenceEqual(frame.Payload.ToArray()));

            Assert.AreEqual(4, subject.Frames[1].Length);
            Assert.IsTrue(new byte[] { 2, 3, 4 }.SequenceEqual(subject.Frames[1].Payload.ToArray()));
        }

        [TestMethod]
        public async Task ReadFromStreamTest()
        {
            var payloads = new byte[][]
            {
                new byte[] { 1,2,3 },
                new byte[] { 2,3,4 },
                new byte[] { 3,4,5 }
            };

            var stream = new MemoryStream();

            await LengthCodeHelper.Write7BitEncodedIntAsync(stream, payloads.Sum(p => p.Length + 1));

            foreach (var payload in payloads)
            {
                await LengthCodeHelper.Write7BitEncodedIntAsync(stream, payload.Length);
                stream.Write(payload, 0, payload.Length);
            }

            stream.Position = 0;

            var subject = await ValueMessage.ReadFromStreamAsync(stream, default);

            Assert.AreEqual(payloads.Sum(p => p.Length + 1) + 1, subject.Length);
            Assert.AreEqual(3, subject.Frames.Count);

            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(payloads[i].Length + 1, subject.Frames[i].Length);
                Assert.IsTrue(payloads[i].SequenceEqual(subject.Frames[i].Payload.ToArray()));
            }
        }

        [TestMethod]
        public async Task ReadFromMemoryTest()
        {
            var payloads = new byte[][]
            {
                new byte[] { 1,2,3 },
                new byte[] { 2,3,4 },
                new byte[] { 3,4,5 }
            };

            var stream = new MemoryStream();

            await LengthCodeHelper.Write7BitEncodedIntAsync(stream, payloads.Sum(p => p.Length + 1));

            foreach (var payload in payloads)
            {
                await LengthCodeHelper.Write7BitEncodedIntAsync(stream, payload.Length);
                stream.Write(payload, 0, payload.Length);
            }

            var memory = stream.ToArray();

            var subject = ValueMessage.ReadFromMemory(memory.AsSpan());

            Assert.AreEqual(payloads.Sum(p => p.Length + 1) + 1, subject.Length);
            Assert.AreEqual(3, subject.Frames.Count);

            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(payloads[i].Length + 1, subject.Frames[i].Length);
                Assert.IsTrue(payloads[i].SequenceEqual(subject.Frames[i].Payload.ToArray()));
            }
        }

        [TestMethod]
        public async Task WriteToStreamTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessage(frames);

            var stream = new MemoryStream();
            await ValueMessage.WriteToStreamAsync(subject, stream, default);

            stream.Position = 0;

            Assert.AreEqual(frames.Sum(p => p.Length), await LengthCodeHelper.Read7BitEncodedIntAsync(stream));

            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(3, await LengthCodeHelper.Read7BitEncodedIntAsync(stream));
                var buffer = new byte[3];

                stream.Read(buffer, 0, 3);

                Assert.IsTrue(buffer.SequenceEqual(frames[i].Payload.ToArray()));
            }
        }

        [TestMethod]
        public async Task WriteToMemoryTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessage(frames);
            var memory = new byte[subject.Length];

            ValueMessage.WriteToMemory(subject, memory.AsSpan());

            var stream = new MemoryStream(memory);

            Assert.AreEqual(frames.Sum(p => p.Length), await LengthCodeHelper.Read7BitEncodedIntAsync(stream));

            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(3, await LengthCodeHelper.Read7BitEncodedIntAsync(stream));
                var buffer = new byte[3];

                stream.Read(buffer, 0, 3);

                Assert.IsTrue(buffer.SequenceEqual(frames[i].Payload.ToArray()));
            }
        }

        [TestMethod]
        public async Task ValueMessageRoundtripTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessage(frames);
            var stream = new MemoryStream();
            await ValueMessage.WriteToStreamAsync(subject, stream, default);

            stream.Position = 0;
            var subject2 = await ValueMessage.ReadFromStreamAsync(stream, default);

            Assert.AreEqual(subject.Length, subject2.Length);
            Assert.AreEqual(subject.Frames.Count, subject2.Frames.Count);

            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(subject.Frames[i].Length, subject2.Frames[i].Length);
                Assert.IsTrue(subject.Frames[i].Payload.ToArray().SequenceEqual(subject2.Frames[i].Payload.ToArray()));
            }
        }
    }
}
