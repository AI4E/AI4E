/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

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
