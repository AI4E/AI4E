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

namespace AI4E.Messaging.MessageHandlers
{
    /// <summary>
    /// Represents a message handler application feature.
    /// </summary>
    public class MessageHandlerFeature
    {
        /// <summary>
        /// Gets the list of types that are message handlers.
        /// </summary>
        public IList<Type> MessageHandlers { get; } = new List<Type>();
    }

    /// <summary>
    /// Represents a message handler feature provider.
    /// </summary>
    public class MessageHandlerFeatureProvider : IApplicationFeatureProvider<MessageHandlerFeature>
    {
        /// <inheritdoc/>
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

        private bool IsMessageHandler(Type type, bool allowAbstract)
        {
            if (type.IsInterface || type.IsEnum)
                return false;

            if (!allowAbstract && type.IsAbstract)
                return false;

            if (type.ContainsGenericParameters)
                return false;

            if (type.IsDefined<NoMessageHandlerAttribute>(inherit: false))
                return false;

            if (type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) && type.IsPublic)
                return true;

            if (type.IsDefined<MessageHandlerAttribute>(inherit: false))
                return true;

            return type.BaseType != null && IsMessageHandler(type.BaseType, allowAbstract: true);
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified type is a message handler.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if <paramref name="type"/> is a message handler, false otherwise.</returns>
        public virtual bool IsMessageHandler(Type type)
        {
            return IsMessageHandler(type, allowAbstract: false);
        }

        internal static void Configure(ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<MessageHandlerFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new MessageHandlerFeatureProvider());
            }
        }
    }
}
