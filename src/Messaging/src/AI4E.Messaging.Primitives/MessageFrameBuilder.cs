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

namespace AI4E.Messaging
{
    public sealed class MessageFrameBuilder
    {
        private ReadOnlyMemory<byte> _payload;

        public MessageFrameBuilder()
        {
            _payload = Array.Empty<byte>(); // TODO: Can we use the default value of ReadOnlyMemory<byte> here?
        }

        public MessageFrameBuilder(in MessageFrame frame)
        {
            _payload = frame.Payload;
        }

        public MessageFrameBuilder(in ReadOnlyMemory<byte> payload)
        {
            if (payload.IsEmpty)
            {
                _payload = Array.Empty<byte>(); // TODO: Can we use the default value of ReadOnlyMemory<byte> here?
            }
            else
            {
                // Set property to ensure we copy the payload to a new array.
                Payload = payload;
            }
        }

        public int Length => BuildMessageFrame().Length;

        public ReadOnlyMemory<byte> Payload
        {
            get => _payload;
            set => _payload = value.CopyToArray();
        }

        public void UnsafeReplacePayloadWithoutCopy(ReadOnlyMemory<byte> payload)
        {
            _payload = payload;
        }

        public MessageFrame BuildMessageFrame()
        {
            return MessageFrame.UnsafeCreateWithoutCopy(Payload);
        }

        public MessageFrameBuilderStream OpenStream(bool overrideContent = false)
        {
            return new MessageFrameBuilderStream(this, overrideContent);
        }
    }
}
