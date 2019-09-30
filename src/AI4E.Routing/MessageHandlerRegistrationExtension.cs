using System;
using AI4E.Messaging;

namespace AI4E.Routing
{
    public static class MessageHandlerRegistrationExtension
    {
        public static bool IsTransient(this IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            var config = handlerRegistration.Configuration;
            return handlerRegistration.Configuration.IsEnabled<IsTransientMessageHandlerConfiguration>();
        }
    }
}
