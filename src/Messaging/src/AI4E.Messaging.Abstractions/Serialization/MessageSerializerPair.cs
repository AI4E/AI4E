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
using System.Diagnostics.CodeAnalysis;
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Messaging.Serialization
{
    public sealed class MessageSerializerPair : IMessageSerializer
    {
        private readonly IMessageSerializer _serializer;
        private readonly IMessageSerializer _deserializer;

        public MessageSerializerPair(IMessageSerializer serializer, IMessageSerializer deserializer)
        {
            if (serializer is null)
                throw new ArgumentNullException(nameof(serializer));

            if (deserializer is null)
                throw new ArgumentNullException(nameof(deserializer));

            _serializer = serializer;
            _deserializer = deserializer;
        }

        public Message Serialize(IDispatchResult dispatchResult)
        {
            return _serializer.Serialize(dispatchResult);
        }

        public Message Serialize(DispatchDataDictionary dispatchData)
        {
            return _serializer.Serialize(dispatchData);
        }

        public bool TryDeserialize(Message message, [NotNullWhen(true)] out IDispatchResult? dispatchResult)
        {
            return _deserializer.TryDeserialize(message, out dispatchResult);
        }

        public bool TryDeserialize(Message message, [NotNullWhen(true)] out DispatchDataDictionary? dispatchData)
        {
            return _deserializer.TryDeserialize(message, out dispatchData);
        }
    }
}
