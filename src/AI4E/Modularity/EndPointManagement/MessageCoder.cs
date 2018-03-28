using System;
using System.IO;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.EndPointManagement
{
    public sealed class MessageCoder<TAddress> : IMessageCoder<TAddress>
    {
        private readonly IRouteSerializer _routeSerializer;
        private readonly IAddressConversion<TAddress> _addressSerializer;

        public MessageCoder(IRouteSerializer routeSerializer, IAddressConversion<TAddress> addressSerializer)
        {
            if (routeSerializer == null)
                throw new ArgumentNullException(nameof(routeSerializer));

            if (addressSerializer == null)
                throw new ArgumentNullException(nameof(addressSerializer));

            _routeSerializer = routeSerializer;
            _addressSerializer = addressSerializer;
        }

        public (IMessage message, TAddress localAddress,
                TAddress remoteAddress, EndPointRoute remoteEndPoint,
                EndPointRoute localEndPoint, MessageType messageType) DecodeMessage(IMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = default(MessageType);
            var frameIdx = message.FrameIndex;

            byte[] remoteRouteBytes, remoteAddressBytes, localRouteBytes, localAddressBytes;

            try
            {
                using (var frameStream = message.PopFrame().OpenStream())
                using (var reader = new BinaryReader(frameStream))
                {
                    messageType = (MessageType)reader.ReadInt32();

                    var remoteRouteLength = reader.ReadInt32();
                    remoteRouteBytes = reader.ReadBytes(remoteRouteLength);

                    var remoteAddressLength = reader.ReadInt32();
                    remoteAddressBytes = reader.ReadBytes(remoteAddressLength);

                    var localRouteLength = reader.ReadInt32();
                    localRouteBytes = reader.ReadBytes(localRouteLength);

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

            var remoteRoute = remoteRouteBytes.Length > 0 ? _routeSerializer.DeserializeRoute(remoteRouteBytes) : default;
            var remoteAddress = remoteAddressBytes.Length > 0 ? _addressSerializer.DeserializeAddress(remoteAddressBytes) : default;
            var localRoute = localRouteBytes.Length > 0 ? _routeSerializer.DeserializeRoute(localRouteBytes) : default;
            var localAddress = localAddressBytes.Length > 0 ? _addressSerializer.DeserializeAddress(localAddressBytes) : default;

            return (message, localAddress, remoteAddress, remoteRoute, localRoute, messageType);
        }

        public IMessage EncodeMessage(IMessage message, TAddress localAddress,
                                      TAddress remoteAddress, EndPointRoute remoteEndPoint,
                                      EndPointRoute localEndPoint, MessageType messageType)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var serializedLocalAddress = localAddress == null || localAddress.Equals(default) ? new byte[0] : _addressSerializer.SerializeAddress(localAddress);
            var serializedLocalRoute = localEndPoint == null ? new byte[0] : _routeSerializer.SerializeRoute(localEndPoint);
            var serializedRemoteAddress = remoteAddress == null || remoteAddress.Equals(default) ? new byte[0] : _addressSerializer.SerializeAddress(remoteAddress);
            var serializedRemoteRoute = remoteEndPoint == null ? new byte[0] : _routeSerializer.SerializeRoute(remoteEndPoint);
            var frameIdx = message.FrameIndex;

            try
            {
                using (var frameStream = message.PushFrame().OpenStream(overrideContent: true))
                using (var writer = new BinaryWriter(frameStream))
                {
                    writer.Write((int)messageType);                  // Message type            -- 4 Byte
                    writer.Write(serializedLocalRoute.Length);       // Local route length      -- 4 Byte
                    writer.Write(serializedLocalRoute);              // Local route             -- (Local route length Byte)
                    writer.Write(serializedLocalAddress.Length);     // Local address length    -- 4 Byte
                    writer.Write(serializedLocalAddress);            // Local address           -- (Local address length Byte)
                    writer.Write(serializedRemoteRoute.Length);      // Remote route length     -- 4 Byte
                    writer.Write(serializedRemoteRoute);             // Remote route            -- (Remote route length Byte)
                    writer.Write(serializedRemoteAddress.Length);    // Remote address length   -- 4 Byte
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

    public interface IMessageCoder<TAddress>
    {
        (IMessage message, // TODO: We seriously need a struct for the decoded message
         TAddress localAddress,
         TAddress remoteAddress,
         EndPointRoute remoteEndPoint,
         EndPointRoute localEndPoint,
         MessageType messageType) DecodeMessage(IMessage message);

        IMessage EncodeMessage(IMessage message,
                               TAddress localAddress,
                               TAddress remoteAddress,
                               EndPointRoute remoteEndPoint,
                               EndPointRoute localEndPoint,
                               MessageType messageType);
    }

    public static class MessageCoderExtension
    {
        public static IMessage EncodeMessage<TAddress>(this IMessageCoder<TAddress> messageCoder,
                                                       TAddress localAddress,
                                                       TAddress remoteAddress, EndPointRoute remoteEndPoint,
                                                       EndPointRoute localEndPoint, MessageType messageType)
        {
            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            return messageCoder.EncodeMessage(new Message(), localAddress, remoteAddress, remoteEndPoint, localEndPoint, messageType);
        }
    }
}
