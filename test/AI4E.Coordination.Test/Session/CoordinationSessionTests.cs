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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination.Session
{
    [TestClass]
    public class CoordinationSessionTests
    {
        [TestMethod]
        public void DefaultTest()
        {
            var session = default(SessionIdentifier);

            Assert.IsTrue(session.Prefix.Span.IsEmpty);
            Assert.IsTrue(session.PhysicalAddress.Span.IsEmpty);
        }

        [TestMethod]
        public void CreateTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();

            var session = new SessionIdentifier(prefix.Span, physicalAddress.Span);

            Assert.IsTrue(prefix.Span.SequenceEqual(session.Prefix.Span));
            Assert.IsTrue(physicalAddress.Span.SequenceEqual(session.PhysicalAddress.Span));
        }

        [TestMethod]
        public void CreateWithoutPrefixTest()
        {
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();

            var session = new SessionIdentifier(ReadOnlySpan<byte>.Empty, physicalAddress.Span);

            Assert.IsTrue(session.Prefix.Span.IsEmpty);
            Assert.IsTrue(physicalAddress.Span.SequenceEqual(session.PhysicalAddress.Span));
        }

        [TestMethod]
        public void CreateWithoutPhysicalAddressEqualsDefaultTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();

            var session = new SessionIdentifier(prefix.Span, ReadOnlySpan<byte>.Empty);

            Assert.IsTrue(session.Prefix.Span.IsEmpty);
            Assert.IsTrue(session.PhysicalAddress.Span.IsEmpty);
        }

        [TestMethod]
        public void EqualsSelfTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();

            var session = new SessionIdentifier(prefix.Span, physicalAddress.Span);

            Assert.IsTrue(session.Equals(session));
            Assert.IsTrue(session.Equals((object)session));
#pragma warning disable CS1718
            Assert.IsTrue(session == session);
            Assert.IsFalse(session != session);
#pragma warning restore CS1718     
        }

        [TestMethod]
        public void UnequalsNullTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();

            var session = new SessionIdentifier(prefix.Span, physicalAddress.Span);
            Assert.IsFalse(session.Equals(null));
        }

        [TestMethod]
        public void EqualsFromEqualParametersTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();
            var session = new SessionIdentifier(prefix.Span, physicalAddress.Span);

            var prefix2 = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress2 = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();
            var session2 = new SessionIdentifier(prefix2.Span, physicalAddress2.Span);

            Assert.IsTrue(session.Equals(session2));
            Assert.IsTrue(session.Equals((object)session2));
            Assert.IsTrue(session == session2);
            Assert.IsFalse(session != session2);
        }

        [TestMethod]
        public void HashCodeForEqualParametersTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();
            var session = new SessionIdentifier(prefix.Span, physicalAddress.Span);

            var prefix2 = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress2 = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();
            var session2 = new SessionIdentifier(prefix2.Span, physicalAddress2.Span);

            Assert.IsTrue(session.GetHashCode() == session2.GetHashCode());
        }

        [TestMethod]
        public void StringRoundtripTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();
            var session = new SessionIdentifier(prefix.Span, physicalAddress.Span);
            var parsedSession = SessionIdentifier.FromString(session.ToString());

            Assert.IsTrue(prefix.Span.SequenceEqual(parsedSession.Prefix.Span));
            Assert.IsTrue(physicalAddress.Span.SequenceEqual(parsedSession.PhysicalAddress.Span));
        }

        [TestMethod]
        public void CharsRoundtripTest()
        {
            var prefix = Encoding.UTF8.GetBytes("abcdefgh").AsMemory();
            var physicalAddress = Encoding.UTF8.GetBytes("skovmovknmoiM").AsMemory();
            var session = new SessionIdentifier(prefix.Span, physicalAddress.Span);
            var parsedSession = SessionIdentifier.FromChars(session.ToString().AsSpan());

            Assert.IsTrue(prefix.Span.SequenceEqual(parsedSession.Prefix.Span));
            Assert.IsTrue(physicalAddress.Span.SequenceEqual(parsedSession.PhysicalAddress.Span));
        }

        [TestMethod]
        public void DefaultToStringTest()
        {
            var session = default(SessionIdentifier);

            Assert.AreEqual("", session.ToString());
        }

        [TestMethod]
        public void DefaultStringRoundtripTest()
        {
            var session = default(SessionIdentifier);
            var parsedSession = SessionIdentifier.FromString(session.ToString());

            Assert.IsTrue(parsedSession.Prefix.Span.IsEmpty);
            Assert.IsTrue(parsedSession.PhysicalAddress.Span.IsEmpty);
        }

        [TestMethod]
        public void DefaultCharsRoundtripTest()
        {
            var session = default(SessionIdentifier);
            var parsedSession = SessionIdentifier.FromString(session.ToString());

            Assert.IsTrue(parsedSession.Prefix.Span.IsEmpty);
            Assert.IsTrue(parsedSession.PhysicalAddress.Span.IsEmpty);
        }
    }
}
