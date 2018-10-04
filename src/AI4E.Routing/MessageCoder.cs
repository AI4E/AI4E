/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MessageCoder.cs 
 * Types:           AI4E.Routing.MessageCoder'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using AI4E.Remoting;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class MessageCoder<TAddress> : IMessageCoder<TAddress>
    {
        private static readonly byte[] _emptyByteArray = new byte[0];

        private readonly IEndPointAddressSerializer _endPointAddressSerializer;
        private readonly IAddressConversion<TAddress> _addressSerializer;

        public MessageCoder(IEndPointAddressSerializer endPointAddressSerializer, IAddressConversion<TAddress> addressSerializer)
        {
            if (endPointAddressSerializer == null)
                throw new ArgumentNullException(nameof(endPointAddressSerializer));

            if (addressSerializer == null)
                throw new ArgumentNullException(nameof(addressSerializer));

            _endPointAddressSerializer = endPointAddressSerializer;
            _addressSerializer = addressSerializer;
        }

        public (IMessage message, TAddress localAddress,
                TAddress remoteAddress, EndPointAddress remoteEndPoint,
                EndPointAddress localEndPoint, MessageType messageType) DecodeMessage(IMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = default(MessageType);
            var frameIdx = message.FrameIndex;

            byte[] remoteEndPointBytes, remoteAddressBytes, localEndPointBytes, localAddressBytes;

            try
            {
                using (var frameStream = message.PopFrame().OpenStream())
                using (var reader = new BinaryReader(frameStream))
                {
                    messageType = (MessageType)reader.ReadInt32();

                    var remoteEndPointLength = reader.ReadInt32();
                    remoteEndPointBytes = reader.ReadBytes(remoteEndPointLength);

                    var remoteAddressLength = reader.ReadInt32();
                    remoteAddressBytes = reader.ReadBytes(remoteAddressLength);

                    var localEndPointLength = reader.ReadInt32();
                    localEndPointBytes = reader.ReadBytes(localEndPointLength);

                    var localAddressLength = reader.ReadInt32();
                    localAddressBytes = reader.ReadBytes(localAddressLength);
                }
            }
            catch when (message.FrameIndex != frameIdx)
            {
                message.PushFrame();
                Assert(message.FrameIndex == frameIdx);
                throw;
            }

            var remoteEndPoint = remoteEndPointBytes.Length > 0 ? _endPointAddressSerializer.Deserialize(remoteEndPointBytes) : default;
            var remoteAddress = remoteAddressBytes.Length > 0 ? _addressSerializer.DeserializeAddress(remoteAddressBytes) : default;
            var localEndPoint = localEndPointBytes.Length > 0 ? _endPointAddressSerializer.Deserialize(localEndPointBytes) : default;
            var localAddress = localAddressBytes.Length > 0 ? _addressSerializer.DeserializeAddress(localAddressBytes) : default;

            return (message, localAddress, remoteAddress, remoteEndPoint, localEndPoint, messageType);
        }

        public IMessage EncodeMessage(IMessage message, TAddress localAddress,
                                      TAddress remoteAddress, EndPointAddress remoteEndPoint,
                                      EndPointAddress localEndPoint, MessageType messageType)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var serializedLocalAddress = localAddress == null || localAddress.Equals(default) ? _emptyByteArray : _addressSerializer.SerializeAddress(localAddress);
            var serializedLocalEndPoint = localEndPoint == null ? _emptyByteArray : _endPointAddressSerializer.Serialize(localEndPoint);
            var serializedRemoteAddress = remoteAddress == null || remoteAddress.Equals(default) ? _emptyByteArray : _addressSerializer.SerializeAddress(remoteAddress);
            var serializedRemoteEndPoint = remoteEndPoint == null ? _emptyByteArray : _endPointAddressSerializer.Serialize(remoteEndPoint);
            var frameIdx = message.FrameIndex;

            try
            {
                using (var frameStream = message.PushFrame().OpenStream(overrideContent: true))
                using (var writer = new BinaryWriter(frameStream))
                {
                    writer.Write((int)messageType);                  // Message type            -- 4 Byte
                    writer.Write(serializedLocalEndPoint.Length);    // Local ep length         -- 4 Byte
                    writer.Write(serializedLocalEndPoint);           // Local ep                -- (Local ep length Byte)
                    writer.Write(serializedLocalAddress.Length);     // Local address length    -- 4 Byte
                    writer.Write(serializedLocalAddress);            // Local address           -- (Local address length Byte)
                    writer.Write(serializedRemoteEndPoint.Length);   // Remote ep length        -- 4 Byte
                    writer.Write(serializedRemoteEndPoint);          // Remote ep               -- (Remote ep length Byte)
                    writer.Write(serializedRemoteAddress.Length);    // Remote ep length        -- 4 Byte
                    writer.Write(serializedRemoteAddress);           // Remote address          -- (Remote address length Byte)   
                }
            }
            catch when (message.FrameIndex != frameIdx)
            {
                message.PopFrame();
                Assert(message.FrameIndex == frameIdx);
                throw;
            }

            return message;
        }
    }
}
