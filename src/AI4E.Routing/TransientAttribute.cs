using System;
using AI4E.Messaging;

namespace AI4E.Routing
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class TransientAttribute : ConfigureMessageHandlerAttribute
    {
        public TransientAttribute() : this(true) { }

        public TransientAttribute(bool isTransient)
        {
            IsTransient = isTransient;
        }

        public bool IsTransient { get; }

        protected override void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IMessageHandlerConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Configure(() => new IsTransientMessageHandlerConfiguration(IsTransient));
        }
    }

    public sealed class IsTransientMessageHandlerConfiguration : IMessageHandlerConfigurationFeature
    {
        public IsTransientMessageHandlerConfiguration(bool isTransient)
        {
            IsTransient = isTransient;
        }

        public bool IsTransient { get; }

        bool IMessageHandlerConfigurationFeature.IsEnabled => IsTransient;
    }
}
