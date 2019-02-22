using System;

namespace AI4E
{
    public static class MessageHandlerRegistrationExtension
    {
        public static bool IsPublishOnly(this IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            return handlerRegistration.Configuration.IsEnabled<PublishOnlyMessageHandlerConfiguration>();
        }

        public static bool IsLocalDispatchOnly(this IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            return handlerRegistration.Configuration.IsEnabled<LocalDispatchOnlyMessageHandlerConfiguration>();
        }
    }
}
