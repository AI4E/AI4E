using System;
using System.Linq;

namespace AI4E.Routing
{
    public static class MessageHandlerRegistrationExtension
    {
        public static bool IsTransient(this IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            var config = handlerRegistration.Configuration;

            if (config.Count == 0)
                return false;

            var configEntry = config.OfType<TransientAttribute>().FirstOrDefault();
            return configEntry != null && configEntry.IsTransient;
        }
    }
}
