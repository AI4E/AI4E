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
using System.Text;
using System.Threading;
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Mocks
{
    public sealed class SessionIdentifierProviderMock : ISessionIdentifierProvider
    {
        private readonly List<SessionIdentifier> _coordinationSessions = new List<SessionIdentifier>();
        private int _counter;

        public SessionIdentifier CreateUniqueSessionIdentifier()
        {
            var prefix = BitConverter.GetBytes(Interlocked.Increment(ref _counter));
            var physicalAddress = Encoding.UTF8.GetBytes("Testaddress");

            var result = new SessionIdentifier(prefix, physicalAddress);
            _coordinationSessions.Add(result);
            return result;
        }

        public IReadOnlyList<SessionIdentifier> CreatedSessions => _coordinationSessions;
    }
}
