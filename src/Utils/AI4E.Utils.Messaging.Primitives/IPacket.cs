namespace AI4E.Utils.Messaging.Primitives
{
    public interface IPacket
    {
        Message Message { get; }

        IPacket WithMessage(in Message message);
    }

    public interface IPacket<TPacket> : IPacket
        where TPacket : IPacket
    {
        new TPacket WithMessage(in Message message);

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        IPacket IPacket.WithMessage(in Message message)
        {
            return WithMessage(message);
        }
#endif
    }

    public readonly struct Packet : IPacket<Packet>
    {
        public Packet(Message message)
        {
            Message = message;
        }

        public Packet WithMessage(in Message message)
        {
            return new Packet(message);
        }

        public Message Message { get; }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        IPacket IPacket.WithMessage(in Message message)
        {
            return new Packet(message);
        }
#endif
    }
}
