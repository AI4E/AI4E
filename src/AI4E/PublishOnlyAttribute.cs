using System;
using System.Collections.Generic;
using System.Linq;
using static System.Diagnostics.Debug;

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

        protected override void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IList<object> configuration)
        {
            var existing = configuration.OfType<PublishOnlyAttribute>().FirstOrDefault();

            if (existing != null)
            {
                configuration.Remove(existing);

                Assert(!configuration.OfType<PublishOnlyAttribute>().Any());
            }

            configuration.Add(this);
        }
    }
}
