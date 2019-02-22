using System;

namespace AI4E
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class ConfigureMessageHandlerAttribute : Attribute
    {
        protected abstract void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder);

        public void ExecuteConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            if (configurationBuilder == null)
                throw new ArgumentNullException(nameof(configurationBuilder));

            ConfigureMessageHandler(memberDescriptor, configurationBuilder);
        }
    }
}
