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

namespace AI4E.Handler
{
    public sealed class MessageDispatchContext : IMessageDispatchContext
    {
        public MessageDispatchContext(
            IServiceProvider dispatchServices,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool isLocalDispatch)
        {
            if (dispatchServices == null)
                throw new ArgumentNullException(nameof(dispatchServices));

            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            DispatchServices = dispatchServices;
            DispatchData = dispatchData;
            IsPublish = publish;
            IsLocalDispatch = isLocalDispatch;
        }

        public IServiceProvider DispatchServices { get; }
        public DispatchDataDictionary DispatchData { get; }
        public bool IsPublish { get; }
        public bool IsLocalDispatch { get; }
    }
}
