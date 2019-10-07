namespace AI4E.Utils.Messaging.Primitives
{
    public interface IPacket
    {
        ValueMessage Message { get; }

        IPacket WithMessage(in ValueMessage message);
    }

    public interface IPacket<TPacket> : IPacket
        where TPacket : IPacket
    {
        new TPacket WithMessage(in ValueMessage message);

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        IPacket IPacket.WithMessage(in ValueMessage message)
        {
            return WithMessage(message);
        }
#endif
    }

    public readonly struct Packet : IPacket<Packet>
    {
        public Packet(ValueMessage message)
        {
            Message = message;
        }

        public Packet WithMessage(in ValueMessage message)
        {
            return new Packet(message);
        }

        public ValueMessage Message { get; }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        IPacket IPacket.WithMessage(in ValueMessage message)
        {
            return new Packet(message);
        }
#endif
    }
}
