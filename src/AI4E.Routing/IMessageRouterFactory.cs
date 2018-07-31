namespace AI4E.Routing
{
    public interface IMessageRouterFactory
    {
        IMessageRouter CreateMessageRouter(ISerializedMessageHandler serializedMessageHandler);
    }
}