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

namespace AI4E.Utils.Messaging.Primitives
{
    [TestClass]
    public sealed class ValueMessageFrameBuilderTests
    {
        [TestMethod]
        public void CreateTest()
        {
            var subject = new ValueMessageFrameBuilder();

            Assert.IsTrue(subject.Payload.IsEmpty);
            Assert.AreEqual(1, subject.Length);
        }

        [TestMethod]
        public void CreateFromFrameTest()
        {
            var frame = BuildFrame();
            var subject = new ValueMessageFrameBuilder(frame);

            Assert.AreEqual(frame.Length, subject.Length);
            Assert.IsTrue(frame.Payload.ToArray().SequenceEqual(subject.Payload.ToArray()));
        }

        [TestMethod]
        public void BuildMessageFrameTest()
        {
            var frame = BuildFrame();
            var subject = new ValueMessageFrameBuilder(frame);

            var builtFrame = subject.BuildMessageFrame();

            Assert.AreEqual(frame.Length, builtFrame.Length);
            Assert.IsTrue(frame.Payload.ToArray().SequenceEqual(builtFrame.Payload.ToArray()));
        }

        [TestMethod]
        public void ReplacePayloadAfterBuildMessageFrameTest()
        {
            var frame = BuildFrame();
            var subject = new ValueMessageFrameBuilder(frame);

            var builtFrame = subject.BuildMessageFrame();

            subject.UnsafeReplacePayloadWithoutCopy(new byte[] { 1, 2, 3 });

            Assert.AreEqual(frame.Length, builtFrame.Length);
            Assert.IsTrue(frame.Payload.ToArray().SequenceEqual(builtFrame.Payload.ToArray()));
        }

        [TestMethod]
        public void ModifyPayloadAfterBuildMessageFrameTest()
        {
            var subject = new ValueMessageFrameBuilder();
            var payload = new byte[] { 1, 2, 3 };

            subject.Payload = payload;

            var builtFrame = subject.BuildMessageFrame();

            payload[0] = 55;

            Assert.AreEqual(4, builtFrame.Length);
            Assert.IsTrue(new byte[] { 1, 2, 3 }.SequenceEqual(builtFrame.Payload.ToArray()));
        }

        private static ValueMessageFrame BuildFrame()
        {
            var payload = Enumerable.Range(0, 45).Select(p => unchecked((byte)p)).ToArray();
            return new ValueMessageFrame(payload);
        }
    }
}
