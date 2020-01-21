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

namespace AI4E.Messaging.Serialization
{
    public static class MessageSerializerExtension
    {
        public static TDispatchResult? Roundtrip<TDispatchResult>(
            this IMessageSerializer messageSerializer,
            TDispatchResult dispatchResult)
            where TDispatchResult : class, IDispatchResult
        {
#pragma warning disable CA1062
            var message = messageSerializer.Serialize(dispatchResult);
#pragma warning restore CA1062

            if (!messageSerializer.TryDeserialize(message, out IDispatchResult? deserializedDispatchResult))
            {
                deserializedDispatchResult = null;
            }

            return deserializedDispatchResult as TDispatchResult;
        }

        public static DispatchDataDictionary? Roundtrip(
            this IMessageSerializer messageSerializer,
            DispatchDataDictionary dispatchData)
        {
#pragma warning disable CA1062
            var message = messageSerializer.Serialize(dispatchData);
#pragma warning restore CA1062

            if (!messageSerializer.TryDeserialize(message, out dispatchData!))
            {
                dispatchData = null!;
            }

            return dispatchData;
        }
    }
}
