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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AI4E.Remoting.Mocks;
using AI4E.Remoting.Utils;
using AI4E.Utils.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting
{
    [TestClass]
    public class PhysicalEndPointMultiplexerTests
    {
        public TestMessagingSystem MessagingSystem { get; set; }
        public IPhysicalEndPoint<TestMessagingSystemAddress> PhysicalEndPoint1 { get; set; }
        public IPhysicalEndPoint<TestMessagingSystemAddress> PhysicalEndPoint2 { get; set; }
        public PhysicalEndPointMultiplexer<TestMessagingSystemAddress> PhysicalEndPointMultiplexer1 { get; set; }
        public PhysicalEndPointMultiplexer<TestMessagingSystemAddress> PhysicalEndPointMultiplexer2 { get; set; }

        [TestInitialize]
        public void Setup()
        {
            MessagingSystem = new TestMessagingSystem();
            PhysicalEndPoint1 = MessagingSystem.CreatePhysicalEndPoint();
            PhysicalEndPoint2 = MessagingSystem.CreatePhysicalEndPoint();
            PhysicalEndPointMultiplexer1 = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(PhysicalEndPoint1);
            PhysicalEndPointMultiplexer2 = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(PhysicalEndPoint2);
        }

        [TestMethod]
        public async Task EncodeMultiplexNameTest()
        {
            var physicalEndPoint = new PhysicalEndPointMock<TestMessagingSystemAddress>(new TestMessagingSystemAddress(1));
            using var physicalEndPointMultiplexer = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(physicalEndPoint);
            var multiplexPhysicalEndPoint = physicalEndPointMultiplexer.GetPhysicalEndPoint("multiplexName");

            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var txMessage = ValueMessage.FromFrames(payload);
            var txTransmission = new Transmission<TestMessagingSystemAddress>(
                txMessage,
                new TestMessagingSystemAddress(2));

            await multiplexPhysicalEndPoint.SendAsync(txTransmission);

            var rxTransmission = physicalEndPoint.TxQueue.Single();
            var multiplexNameBytes = Encoding.UTF8.GetBytes("multiplexName");

            Assert.AreEqual(new TestMessagingSystemAddress(2), rxTransmission.RemoteAddress);
            Assert.AreEqual(2, rxTransmission.Message.Frames.Count);

            var rxMessage = rxTransmission.Message;
            rxMessage = rxMessage.PopFrame(out var frame);

            using (var frameStream = frame.OpenStream())
            using (var reader = new BinaryReader(frameStream))
            {
                Assert.AreEqual(multiplexNameBytes.Length, reader.ReadInt32());
                Assert.IsTrue(multiplexNameBytes.SequenceEqual(reader.ReadBytes(multiplexNameBytes.Length)));
            }

            rxMessage.PopFrame(out frame);
            Assert.IsTrue(frame.Payload.Span.SequenceEqual(payload));
        }

        [TestMethod]
        public async Task DecodeMultiplexNameTest()
        {
            var physicalEndPoint = new PhysicalEndPointMock<TestMessagingSystemAddress>(new TestMessagingSystemAddress(1));
            using var physicalEndPointMultiplexer = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(physicalEndPoint);
            var multiplexPhysicalEndPoint = physicalEndPointMultiplexer.GetPhysicalEndPoint("multiplexName");

            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var txMessageBuilder = new ValueMessageBuilder();
            txMessageBuilder.PushFrame(payload);

            var multiplexNameBytes = Encoding.UTF8.GetBytes("multiplexName");

            using (var frameStream = txMessageBuilder.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write(multiplexNameBytes.Length);
                writer.Write(multiplexNameBytes);
            }

            var txMessage = txMessageBuilder.BuildMessage();
            var txTransmission = new Transmission<TestMessagingSystemAddress>(
               txMessage,
               new TestMessagingSystemAddress(2));

            physicalEndPoint.RxQueue.Enqueue(txTransmission);

            var transmission = await multiplexPhysicalEndPoint.ReceiveAsync();

            Assert.AreEqual(new TestMessagingSystemAddress(2), transmission.RemoteAddress);
            Assert.AreEqual(2, transmission.Message.Frames.Count);

            transmission.Message.PopFrame(out var frame);

            Assert.IsTrue(frame.Payload.Span.SequenceEqual(payload));
        }

        [TestMethod]
        public void MultiplexPhysicalEndPointLocalAddressTest()
        {
            var multiplexPhysicalEndPoint = PhysicalEndPointMultiplexer1.GetPhysicalEndPoint("multiplex");

            Assert.AreEqual(PhysicalEndPoint1.LocalAddress, multiplexPhysicalEndPoint.LocalAddress);
        }

        [TestMethod]
        public async Task TransmissionTest()
        {
            var multiplexPhysicalEndPoint1 = PhysicalEndPointMultiplexer1.GetPhysicalEndPoint("multiplex");
            var multiplexPhysicalEndPoint2 = PhysicalEndPointMultiplexer2.GetPhysicalEndPoint("multiplex");
            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var txMessage = ValueMessage.FromFrames(payload);
            var txTransmission = new Transmission<TestMessagingSystemAddress>(
               txMessage,
               multiplexPhysicalEndPoint2.LocalAddress);




            await multiplexPhysicalEndPoint1.SendAsync(txTransmission);
            var rxTransmission = await multiplexPhysicalEndPoint2.ReceiveAsync();

            Assert.AreEqual(multiplexPhysicalEndPoint1.LocalAddress, rxTransmission.RemoteAddress);

            rxTransmission.Message.PopFrame(out var frame);

            Assert.IsTrue(frame.Payload.Span.SequenceEqual(payload));
        }

        // TODO: Test cancellation, disposal, end-point deallocation
    }
}
