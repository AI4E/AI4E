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
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Remoting
{
    public static class MessageConversion
    {
        [Obsolete]
        public static ValueMessage ToValueMessage(this IMessage message)
        {
            var frameIndex = message.FrameIndex;
            try
            {
                var frameCount = message.FrameIndex + 1;
                var frames = new ValueMessageFrame[frameCount];

                for (var i = 0; i < frameCount; i++)
                {
                    var frame = message.PopFrame();
                    using var frameStream = frame.OpenStream();
                    var buffer = new byte[frameStream.Length];
                    frameStream.ReadExact(buffer);
                    frames[frameCount - i - 1] = ValueMessageFrame.UnsafeCreateWithoutCopy(buffer);
                }

                return new ValueMessage(frames);
            }
            finally
            {
                while (message.FrameIndex != frameIndex)
                {
                    message.PushFrame();
                }
            }
        }

        [Obsolete]
        public static IMessage ToMessage(in this ValueMessage valueMessage)
        {
            var result = new Message();

            foreach (var frame in valueMessage.Frames)
            {
                var resultFrame = result.PushFrame();
                using var frameStream = resultFrame.OpenStream();
                frameStream.Write(frame.Payload.Span);
            }

            return result;
        }
    }
}
