namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extension methods for the <see cref="IMessageHandlerRegistration"/> type.
    /// </summary>
    public static class MessageHandlerRegistrationExtensions
    {
        /// <summary>
        /// Gets a boolean value indicating whether the 'publish only' feature is enabled.
        /// </summary>
        /// <param name="handlerRegistration">The message handler registration.</param>
        /// <returns>
        /// True if the 'publish only' feature is enabled for <paramref name="handlerRegistration"/>,
        /// false otherwise.
        /// </returns>
        public static bool IsPublishOnly(this IMessageHandlerRegistration handlerRegistration)
        {
            return handlerRegistration.Configuration.IsEnabled<PublishOnlyMessageHandlerConfiguration>();
        }

        /// <summary>
        /// Gets a boolean value indicating whether the 'local dispatch only' feature is enabled.
        /// </summary>
        /// <param name="handlerRegistration">The message handler registration.</param>
        /// <returns>
        /// True if the 'local dispatch only' feature is enabled for <paramref name="handlerRegistration"/>,
        /// false otherwise.
        /// </returns>
        public static bool IsLocalDispatchOnly(this IMessageHandlerRegistration handlerRegistration)
        {
            return handlerRegistration.Configuration.IsEnabled<LocalDispatchOnlyMessageHandlerConfiguration>();
        }

        public static bool IsTransient(this IMessageHandlerRegistration handlerRegistration)
        {
            return handlerRegistration.Configuration.IsEnabled<IsTransientMessageHandlerConfiguration>();
        }
    }
}
