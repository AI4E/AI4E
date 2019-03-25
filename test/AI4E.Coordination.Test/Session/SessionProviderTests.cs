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
using System.Linq;
using System.Text;
using AI4E.Coordination.Mocks;
using AI4E.Coordination.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination.Session
{
    [TestClass]
    public class SessionProviderTests
    {
        [TestMethod]
        public void GetSessionTest()
        {
            var localAddress = new StringAddress("local-address");
            var endPointMultiplexer = new PhysicalEndPointMultiplexerMock<StringAddress>(localAddress);
            var dateTimeProvider = new DateTimeProviderMock();
            var addressConversion = new StringAddressConversion();
            var sessionProvider = new SessionProvider<StringAddress>(endPointMultiplexer, dateTimeProvider, addressConversion);

            var session = sessionProvider.GetSession();

            Assert.IsTrue(Encoding.UTF8.GetBytes(localAddress.Address).AsSpan().SequenceEqual(session.PhysicalAddress.Span));
            Assert.IsFalse(session.Prefix.IsEmpty);
        }

        [TestMethod]
        public void GetSessionOfSameTimeTest()
        {
            var localAddress = new StringAddress("local-address");
            var endPointMultiplexer = new PhysicalEndPointMultiplexerMock<StringAddress>(localAddress);
            var dateTimeProvider = new DateTimeProviderMock();
            var addressConversion = new StringAddressConversion();
            var sessionProvider = new SessionProvider<StringAddress>(endPointMultiplexer, dateTimeProvider, addressConversion);

            var session1 = sessionProvider.GetSession();
            var session2 = sessionProvider.GetSession();

            Assert.AreNotEqual(session1, session2);
        }
    }
}
