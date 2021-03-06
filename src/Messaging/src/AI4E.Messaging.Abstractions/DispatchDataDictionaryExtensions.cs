﻿/* License
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

namespace AI4E.Messaging
{
    public static class DispatchDataDictionaryExtensions
    {
        public static DispatchDataDictionary<TMessage> As<TMessage>(this DispatchDataDictionary dispatchData)
            where TMessage : class
        {
#pragma warning disable CA1062
            if (!(dispatchData.Message is TMessage message))
#pragma warning restore CA1062
                throw new InvalidOperationException(
                    $"Cannot dispatch a message of type '{dispatchData.MessageType}' to " +
                    $"a handler that handles messages of type '{typeof(TMessage)}'.");

            if (!(dispatchData is DispatchDataDictionary<TMessage> typedDispatchData))
            {
                typedDispatchData = new DispatchDataDictionary<TMessage>(dispatchData.MessageType, message, dispatchData);
            }

            return typedDispatchData;
        }
    }
}
