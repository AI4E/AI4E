using System;
using System.Collections.Generic;
using System.Linq;
using AI4E.Utils.ApplicationParts;
using AI4E.Utils;

namespace AI4E.Handler
{
    public class MessageHandlerFeature
    {
        public IList<Type> MessageHandlers { get; } = new List<Type>();
    }

    public class MessageHandlerFeatureProvider : IApplicationFeatureProvider<MessageHandlerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, MessageHandlerFeature feature)
        {
            foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
            {
                foreach (var type in part.Types)
                {
                    if (IsMessageHandler(type) && !feature.MessageHandlers.Contains(type))
                    {
                        feature.MessageHandlers.Add(type);
                    }
                }
            }
        }

        protected internal virtual bool IsMessageHandler(Type type)
        {
            return (type.IsClass || type.IsValueType && !type.IsEnum) &&
                   !type.IsAbstract &&
                   !type.ContainsGenericParameters &&
                   !type.IsDefined<NoMessageHandlerAttribute>(inherit: false) &&
                   (type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) && type.IsPublic || type.IsDefined<MessageHandlerAttribute>(inherit: false));
        }
    }
}
