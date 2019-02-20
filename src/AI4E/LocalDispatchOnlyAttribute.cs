using System;

namespace AI4E
{
    /// <summary>
    /// Configures a message handler to allow it as target for locally dispatched messages only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class LocalDispatchOnlyAttribute : ConfigureMessageHandlerAttribute
    {
        public LocalDispatchOnlyAttribute() : this(true) { }

        public LocalDispatchOnlyAttribute(bool localDispatchOnly)
        {
            LocalDispatchOnly = localDispatchOnly;
        }

        public bool LocalDispatchOnly { get; }

        protected override void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Configure(() => new LocalDispatchOnlyMessageHandlerConfiguration(LocalDispatchOnly));
        }
    }

    public sealed class LocalDispatchOnlyMessageHandlerConfiguration : IMessageHandlerConfigurationFeature
    {
        public LocalDispatchOnlyMessageHandlerConfiguration(bool localDispatchOnly)
        {
            LocalDispatchOnly = localDispatchOnly;
        }

        public bool LocalDispatchOnly { get; }

        bool IMessageHandlerConfigurationFeature.IsEnabled => LocalDispatchOnly;
    }
}
