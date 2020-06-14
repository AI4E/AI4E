using AI4E.Messaging.Routing;

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
#pragma warning disable CA1062
            return handlerRegistration.Configuration.IsEnabled<PublishOnlyMessageHandlerConfiguration>();
#pragma warning restore CA1062
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
#pragma warning disable CA1062
            return handlerRegistration.Configuration.IsEnabled<LocalDispatchOnlyMessageHandlerConfiguration>();
#pragma warning restore CA1062
        }

        public static bool IsTransient(this IMessageHandlerRegistration handlerRegistration)
        {
#pragma warning disable CA1062
            return handlerRegistration.Configuration.IsEnabled<IsTransientMessageHandlerConfiguration>();
#pragma warning restore CA1062
        }

        public static Route GetRoute(this IMessageHandlerRegistration handlerRegistration)
        {
#pragma warning disable CA1062
            return new Route(handlerRegistration.MessageType);
#pragma warning restore CA1062
        }

        public static RouteRegistrationOptions GetRouteOptions(this IMessageHandlerRegistration handlerRegistration)
        {
            var result = RouteRegistrationOptions.Default;

            if (handlerRegistration.IsPublishOnly())
            {
                result |= RouteRegistrationOptions.PublishOnly;
            }

            if (handlerRegistration.IsTransient())
            {
                result |= RouteRegistrationOptions.Transient;
            }

            if (handlerRegistration.IsLocalDispatchOnly())
            {
                result |= RouteRegistrationOptions.LocalDispatchOnly;
            }

            return result;
        }
    }
}
