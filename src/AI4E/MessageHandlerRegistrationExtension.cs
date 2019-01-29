using System;
using System.Linq;
using AI4E.Utils;

namespace AI4E
{
    public static class MessageHandlerRegistrationExtension
    {
        public static bool IsPublishOnly(this IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            var config = handlerRegistration.Configuration;

            if (config.Count == 0)
                return false;

            var configEntry = config.OfType<PublishOnlyAttribute>().FirstOrDefault();
            return configEntry != null && configEntry.PublishOnly;
        }

        public static bool IsLocalDispatchOnly(this IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            var config = handlerRegistration.Configuration;

            if (config.Count == 0)
                return false;

            var configEntry = config.OfType<LocalDispatchOnlyAttribute>().FirstOrDefault();
            return configEntry != null && configEntry.LocalDispatchOnly;
        }
    }
}
