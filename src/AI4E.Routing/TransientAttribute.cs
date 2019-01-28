using System;
using System.Collections.Generic;
using System.Linq;
using static System.Diagnostics.Debug;

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

        protected override void ConfigureMessageHandler(MessageHandlerActionDescriptor memberDescriptor, IList<object> configuration)
        {
            var existing = configuration.OfType<TransientAttribute>().FirstOrDefault();

            if (existing != null)
            {
                configuration.Remove(existing);

                Assert(!configuration.OfType<TransientAttribute>().Any());
            }

            configuration.Add(this);
        }
    }
}
