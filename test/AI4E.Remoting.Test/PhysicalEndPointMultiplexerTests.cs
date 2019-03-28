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

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AI4E.Remoting.Test.Mocks;
using AI4E.Remoting.Test.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting.Test
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
            var physicalEndPointMultiplexer = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(physicalEndPoint);
            var multiplexPhysicalEndPoint = physicalEndPointMultiplexer.GetPhysicalEndPoint("multiplexName");
            var txMessage = new Message();

            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            using (var frameStream = txMessage.PushFrame().OpenStream())
            {
                frameStream.Write(payload, 0, payload.Length);
            }

            await multiplexPhysicalEndPoint.SendAsync(txMessage, new TestMessagingSystemAddress(2));

            var (rxMessage, remoteAddress) = physicalEndPoint.TxQueue.Single();

            var multiplexNameBytes = Encoding.UTF8.GetBytes("multiplexName");

            Assert.AreEqual(new TestMessagingSystemAddress(2), remoteAddress);
            Assert.AreEqual(2, rxMessage.FrameCount);

            using (var frameStream = rxMessage.PopFrame().OpenStream())
            using (var reader = new BinaryReader(frameStream))
            {
                Assert.AreEqual(multiplexNameBytes.Length, reader.ReadInt32());
                Assert.IsTrue(multiplexNameBytes.SequenceEqual(reader.ReadBytes(multiplexNameBytes.Length)));
            }

            Assert.IsTrue(ToArray(rxMessage.PopFrame().OpenStream()).SequenceEqual(payload));
        }

        [TestMethod]
        public async Task DecodeMultiplexNameTest()
        {
            var physicalEndPoint = new PhysicalEndPointMock<TestMessagingSystemAddress>(new TestMessagingSystemAddress(1));
            var physicalEndPointMultiplexer = new PhysicalEndPointMultiplexer<TestMessagingSystemAddress>(physicalEndPoint);
            var multiplexPhysicalEndPoint = physicalEndPointMultiplexer.GetPhysicalEndPoint("multiplexName");
            var txMessage = new Message();

            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            using (var frameStream = txMessage.PushFrame().OpenStream())
            {
                frameStream.Write(payload, 0, payload.Length);
            }

            var multiplexNameBytes = Encoding.UTF8.GetBytes("multiplexName");

            using (var frameStream = txMessage.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write(multiplexNameBytes.Length);
                writer.Write(multiplexNameBytes);
            }

            physicalEndPoint.RxQueue.Enqueue((txMessage, new TestMessagingSystemAddress(2)));

            var (rxMessage, remoteAddress) = await multiplexPhysicalEndPoint.ReceiveAsync();

            Assert.AreEqual(new TestMessagingSystemAddress(2), remoteAddress);
            Assert.AreEqual(2, rxMessage.FrameCount);
            Assert.IsTrue(ToArray(rxMessage.PopFrame().OpenStream()).SequenceEqual(payload));
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

            var txMessage = new Message();

            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            using (var frameStream = txMessage.PushFrame().OpenStream())
            {
                frameStream.Write(payload, 0, payload.Length);
            }

            await multiplexPhysicalEndPoint1.SendAsync(txMessage, multiplexPhysicalEndPoint2.LocalAddress);
            var (rxMessage, remoteAddress) = await multiplexPhysicalEndPoint2.ReceiveAsync();

            Assert.AreEqual(multiplexPhysicalEndPoint1.LocalAddress, remoteAddress);
            Assert.IsTrue(ToArray(rxMessage.PopFrame().OpenStream()).SequenceEqual(payload));
        }

        // TODO: Test cancellation, disposal, end-point deallocation

        private byte[] ToArray(Stream stream)
        {
            if (!(stream is MemoryStream memoryStream))
            {
                memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
            }
            return memoryStream.ToArray();
        }
    }
}
