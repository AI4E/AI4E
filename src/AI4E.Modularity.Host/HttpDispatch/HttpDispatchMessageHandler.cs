/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

namespace AI4E.Modularity.HttpDispatch
{
    public sealed class HttpDispatchMessageHandler : MessageHandler
    {
        private readonly HttpDispatchTable _table;

        public HttpDispatchMessageHandler(HttpDispatchTable table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            _table = table;
        }

        public IDispatchResult Handle(RegisterHttpPrefix registerPrefix)
        {
            if (_table.Register(registerPrefix.Prefix, registerPrefix.EndPoint))
            {
                return Success();
            }

            return Failure();
        }

        public IDispatchResult Handle(UnregisterHttpPrefix unregisterPrefix)
        {
            if (_table.Unregister(unregisterPrefix.Prefix))
            {
                return Success();
            }

            return Failure();
        }

        public void Handle(EndPointDisconnected endPointDisconnected)
        {
            _table.Unregister(endPointDisconnected.EndPoint);
        }
    }
}
