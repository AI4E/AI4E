﻿/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2019 Andreas Truetschel and contributors.
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

namespace AI4E.Mocks
{
    public sealed class MessageHandlerProviderMock : IMessageHandlerProvider
    {
        private readonly Dictionary<Type, List<IMessageHandlerRegistration>> _handlerRegistrations
            = new Dictionary<Type, List<IMessageHandlerRegistration>>();

        public List<IMessageHandlerRegistration> GetHandlerRegistrations(Type messageType)
        {
            if (!_handlerRegistrations.TryGetValue(messageType, out var result))
            {
                result = new List<IMessageHandlerRegistration>();
                _handlerRegistrations.Add(messageType, result);
            }

            return result;
        }


        IReadOnlyList<IMessageHandlerRegistration> IMessageHandlerProvider.GetHandlerRegistrations(Type messageType)
        {
            return GetHandlerRegistrations(messageType);
        }

        public IReadOnlyList<IMessageHandlerRegistration> GetHandlerRegistrations()
        {
            return _handlerRegistrations.Values.SelectMany(_ => _).ToList();
        }
    }
}
