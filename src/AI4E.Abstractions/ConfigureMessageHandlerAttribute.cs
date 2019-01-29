using System;
using System.Collections.Generic;

namespace AI4E
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class ConfigureMessageHandlerAttribute : Attribute
    {
        protected abstract void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IList<object> configuration);

        public void ExecuteConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IList<object> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            ConfigureMessageHandler(memberDescriptor, configuration);
        }
    }
}
