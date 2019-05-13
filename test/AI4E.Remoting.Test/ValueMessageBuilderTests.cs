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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting
{
    [TestClass]
    public sealed class ValueMessageBuilderTests
    {
        [TestMethod]
        public void CreateTest()
        {
            var subject = new ValueMessageBuilder();

            Assert.AreEqual(1, subject.Length);
            Assert.IsNull(subject.CurrentFrame);
        }

        [TestMethod]
        public void CreatFromMessageTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var message = new ValueMessage(frames);
            var subject = new ValueMessageBuilder(message);

            Assert.AreEqual(message.Length, subject.Length);

            var frame1 = subject.PopFrame();
            var frame2 = subject.PopFrame();
            var frame3 = subject.PopFrame();
            var frame4 = subject.PopFrame();

            Assert.IsTrue(frame1.Payload.ToArray().SequenceEqual(frames[2].Payload.ToArray()));
            Assert.IsTrue(frame2.Payload.ToArray().SequenceEqual(frames[1].Payload.ToArray()));
            Assert.IsTrue(frame3.Payload.ToArray().SequenceEqual(frames[0].Payload.ToArray()));
            Assert.IsNull(frame4);
            Assert.IsNull(subject.CurrentFrame);
        }

        [TestMethod]
        public void CreatFromFramesTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessageBuilder(frames);

            Assert.AreEqual(frames.Sum(p => p.Length) + 1, subject.Length);

            var frame1 = subject.PopFrame();
            var frame2 = subject.PopFrame();
            var frame3 = subject.PopFrame();
            var frame4 = subject.PopFrame();

            Assert.IsTrue(frame1.Payload.ToArray().SequenceEqual(frames[2].Payload.ToArray()));
            Assert.IsTrue(frame2.Payload.ToArray().SequenceEqual(frames[1].Payload.ToArray()));
            Assert.IsTrue(frame3.Payload.ToArray().SequenceEqual(frames[0].Payload.ToArray()));
            Assert.IsNull(frame4);
            Assert.IsNull(subject.CurrentFrame);
        }

        [TestMethod]
        public void GetLengthTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }), // Length: 4
                new ValueMessageFrame(new byte[] { 2,3,4 }), // Length: 4
                new ValueMessageFrame(new byte[] { 3,4,5 }) // This get overridden
            };

            var subject = new ValueMessageBuilder(frames);
            subject.PopFrame();
            subject.PushFrame().UnsafeReplacePayloadWithoutCopy(new byte[] { 9, 9, 9, 9, 9 }); // Length: 6
            subject.PushFrame().UnsafeReplacePayloadWithoutCopy(new byte[] { 8 }); // Length: 2

            Assert.AreEqual(17, subject.Length); // 4 + 4 + 6 + 2 ( + 1 )
        }

        [TestMethod]
        public void TrimTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessageBuilder(frames);
            subject.PopFrame();
            subject.Trim();

            Assert.AreEqual(9, subject.Length);

            var frame = subject.PushFrame();

            Assert.AreEqual(0, frame.Payload.Length);
        }

        [TestMethod]
        public void ClearTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessageBuilder(frames);
            subject.Clear();

            Assert.AreEqual(1, subject.Length);
            Assert.IsNull(subject.CurrentFrame);
        }

        [TestMethod]
        public void BuildMessageTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessageBuilder(frames);
            subject.PopFrame();

            var message = subject.BuildMessage();

            Assert.AreEqual(9, message.Length);
            Assert.AreEqual(2, message.Frames.Count);

            Assert.IsTrue(message.Frames[1].Payload.ToArray().SequenceEqual(frames[1].Payload.ToArray()));
            Assert.IsTrue(message.Frames[0].Payload.ToArray().SequenceEqual(frames[0].Payload.ToArray()));
        }

        [TestMethod]
        public void BuildMessageNoTrimTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var subject = new ValueMessageBuilder(frames);
            subject.PopFrame();

            var message = subject.BuildMessage(trim: false);

            Assert.AreEqual(13, message.Length);
            Assert.AreEqual(3, message.Frames.Count);

            Assert.IsTrue(message.Frames[2].Payload.ToArray().SequenceEqual(frames[2].Payload.ToArray()));
            Assert.IsTrue(message.Frames[1].Payload.ToArray().SequenceEqual(frames[1].Payload.ToArray()));
            Assert.IsTrue(message.Frames[0].Payload.ToArray().SequenceEqual(frames[0].Payload.ToArray()));
        }

        [TestMethod]
        public void PopEquivalenceTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var message = new ValueMessage(frames);
            var desired = message.PopFrame(out var desiredFrame);
            var subject = message.ToBuilder();
            var frameBuilder = subject.PopFrame();
            var poppedMessage = subject.BuildMessage();

            Assert.AreEqual(desiredFrame.Length, frameBuilder.Length);
            Assert.IsTrue(desiredFrame.Payload.ToArray().SequenceEqual(frameBuilder.Payload.ToArray()));

            Assert.AreEqual(desired.Length, poppedMessage.Length);
            Assert.IsTrue(desired.Frames[0].Payload.ToArray().SequenceEqual(poppedMessage.Frames[0].Payload.ToArray()));
            Assert.IsTrue(desired.Frames[1].Payload.ToArray().SequenceEqual(poppedMessage.Frames[1].Payload.ToArray()));
        }

        [TestMethod]
        public void PushEquivalenceTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
            };

            var frameToPush = new ValueMessageFrame(new byte[] { 3, 4, 5 });

            var message = new ValueMessage(frames);
            var subject = message.ToBuilder();
            var desired = message.PushFrame(frameToPush);
            var frameBuilder = subject.PushFrame();
            frameBuilder.Payload = frameToPush.Payload;
            var pushedMessage = subject.BuildMessage();

            Assert.AreEqual(desired.Length, pushedMessage.Length);
            Assert.IsTrue(desired.Frames[0].Payload.ToArray().SequenceEqual(pushedMessage.Frames[0].Payload.ToArray()));
            Assert.IsTrue(desired.Frames[1].Payload.ToArray().SequenceEqual(pushedMessage.Frames[1].Payload.ToArray()));
            Assert.IsTrue(desired.Frames[2].Payload.ToArray().SequenceEqual(pushedMessage.Frames[2].Payload.ToArray()));
        }
    }
}
