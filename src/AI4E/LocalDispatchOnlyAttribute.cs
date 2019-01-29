using System;
using System.Collections.Generic;
using System.Linq;
using static System.Diagnostics.Debug;

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

        protected override void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IList<object> configuration)
        {
            var existing = configuration.OfType<LocalDispatchOnlyAttribute>().FirstOrDefault();

            if (existing != null)
            {
                configuration.Remove(existing);

                Assert(!configuration.OfType<LocalDispatchOnlyAttribute>().Any());
            }

            configuration.Add(this);
        }
    }
}
