namespace AI4E.Routing
{
    /// <summary>
    /// Represents a factory that can be used to create message routers.
    /// </summary>
    public interface IMessageRouterFactory
    {
        /// <summary>
        /// Creates a message router for the default end point.
        /// </summary>
        /// <param name="serializedMessageHandler">The message handler that is used to handle the messages routed to the caller.</param>
        /// <returns>The created message router.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="serializedMessageHandler"/> is null.</exception>
        IMessageRouter CreateMessageRouter(ISerializedMessageHandler serializedMessageHandler, RouteOptions options);

        /// <summary>
        /// Creates a message router for the specified end point.
        /// </summary>
        /// <param name="serializedMessageHandler">The message handler that is used to handle the messages routed to the caller.</param>
        /// <param name="endPoint">The end point that shall be used.</param>
        /// <returns>The created message router.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="serializedMessageHandler"/> is null.</exception>
        /// <exception cref="System.NotSupportedException">Thrown if the factory is unable to create message routers for end-points other than the default.</exception>
        IMessageRouter CreateMessageRouter(EndPointRoute endPoint, ISerializedMessageHandler serializedMessageHandler, RouteOptions options);
    }
}