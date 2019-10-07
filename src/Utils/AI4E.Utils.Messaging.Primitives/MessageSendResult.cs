namespace AI4E.Utils.Messaging.Primitives
{
    public readonly struct MessageSendResult
    {
        public static MessageSendResult Ack { get; } = new MessageSendResult(default, handled: true);
        public static MessageSendResult NotHandled { get; } = default;

        public MessageSendResult(Message message, bool handled)
        {
            Message = message;
            Handled = handled;
        }

        public Message Message { get; }
        public bool Handled { get; }
    }
}
