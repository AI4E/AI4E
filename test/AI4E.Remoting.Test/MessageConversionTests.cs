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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting
{
    [TestClass]
    public sealed class MessageConversionTests
    {
        [TestMethod]
        public void ToMessageTest()
        {
            var frames = new ValueMessageFrame[]
            {
                new ValueMessageFrame(new byte[] { 1,2,3 }),
                new ValueMessageFrame(new byte[] { 2,3,4 }),
                new ValueMessageFrame(new byte[] { 3,4,5 })
            };

            var source = new ValueMessage(frames);
            var target = source.ToMessage();

            Assert.AreEqual(3, target.FrameCount);
            Assert.AreEqual(2, target.FrameIndex);

            for (var i = 0; i < 3; i++)
            {
                source = source.PopFrame(out var sourceFrame);
                var targetFrame = target.PopFrame();
                using var frameStream = targetFrame.OpenStream();
                var buffer = new byte[3];
                frameStream.ReadExact(buffer);

                Assert.IsTrue(buffer.SequenceEqual(sourceFrame.Payload.ToArray()));
            }

            Assert.IsNull(target.CurrentFrame);
        }

        [TestMethod]
        public void ToValueMessageTest()
        {
            var source = new Message();

            for (var i = 0; i < 4; i++)
            {
                var frame = source.PushFrame();
                using var frameStream = frame.OpenStream();
                var payload = Enumerable.Range(i + 1, 3).Select(p => unchecked((byte)p)).ToArray();
                frameStream.Write(payload);
            }

            source.PopFrame();
            var sourceIndex = source.FrameIndex;
            var target = source.ToValueMessage();

            Assert.AreEqual(sourceIndex, source.FrameIndex);
            Assert.AreEqual(3, target.Frames.Count);

            for (var i = 0; i < 3; i++)
            {
                target = target.PopFrame(out var targetFrame);
                var sourceFrame = source.PopFrame();
                using var frameStream = sourceFrame.OpenStream();
                var buffer = new byte[3];
                frameStream.ReadExact(buffer);

                Assert.IsTrue(buffer.SequenceEqual(targetFrame.Payload.ToArray()));
            }
        }
    }
}
