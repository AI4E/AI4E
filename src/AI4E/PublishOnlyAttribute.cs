using System;

namespace AI4E
{
    /// <summary>
    /// Configures a message handler to allow it as target for published messages only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class PublishOnlyAttribute : ConfigureMessageHandlerAttribute
    {
        public PublishOnlyAttribute() : this(true) { }

        public PublishOnlyAttribute(bool publishOnly)
        {
            PublishOnly = publishOnly;
        }

        public bool PublishOnly { get; }

        protected override void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Configure(() => new PublishOnlyMessageHandlerConfiguration(PublishOnly));
        }
    }

    public sealed class PublishOnlyMessageHandlerConfiguration : IMessageHandlerConfigurationFeature
    {
        public PublishOnlyMessageHandlerConfiguration(bool publishOnly)
        {
            PublishOnly = publishOnly;
        }

        public bool PublishOnly { get; }

        bool IMessageHandlerConfigurationFeature.IsEnabled => PublishOnly;
    }
}
