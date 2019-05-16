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
using System.Net;
using System.Threading.Tasks;
using AI4E.Remoting.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Remoting
{
    [TestClass]
    public class TcpEndPointTests
    {
        [TestMethod]
        public void SetupTest()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPoint = new TcpEndPoint(localAddressResolver, BuildLogger());

            Assert.AreEqual(localAddressResolver.LocalAddress, endPoint.LocalAddress.Address);
            Assert.AreNotEqual(0, endPoint.LocalAddress.Port);
        }

        [TestMethod]
        public async Task LoopbackTest()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPoint = new TcpEndPoint(localAddressResolver, BuildLogger());

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, endPoint.LocalAddress);

            await endPoint.SendAsync(transmission);
            var receivedTransmission = await endPoint.ReceiveAsync();

            Assert.AreEqual(transmission.RemoteAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }

        [TestMethod]
        public async Task TransmissionTest()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPointA = new TcpEndPoint(localAddressResolver, BuildLogger());
            var endPointB = new TcpEndPoint(localAddressResolver, BuildLogger());

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            var receivedTransmission = await endPointB.ReceiveAsync();

            Assert.AreEqual(endPointA.LocalAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }

        [TestMethod]
        public async Task Transmission2Test()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPointA = new TcpEndPoint(localAddressResolver, BuildLogger());

            var portB = new Random().Next(8000, 9000);
            var addressB = new IPEndPoint(localAddressResolver.LocalAddress, portB);

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, addressB);
            var sendTask = endPointA.SendAsync(transmission).AsTask();

            await Task.Delay(20);

            Assert.AreEqual(TaskStatus.WaitingForActivation, sendTask.Status);

            var endPointB = new TcpEndPoint(localAddressResolver, portB, BuildLogger());

            await sendTask;

            var receivedTransmission = await endPointB.ReceiveAsync();

            Assert.AreEqual(endPointA.LocalAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }

        [TestMethod]
        public async Task Transmission3Test()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPointA = new TcpEndPoint(localAddressResolver, BuildLogger());
            var endPointB = new TcpEndPoint(localAddressResolver, BuildLogger());

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            _ = await endPointB.ReceiveAsync();

            message = BuildTestMessage();
            transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            var receivedTransmission = await endPointB.ReceiveAsync();

            Assert.AreEqual(endPointA.LocalAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }


        [TestMethod]
        public async Task SenderConnectionDisposeTest()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPointA = new TcpEndPoint(localAddressResolver, BuildLogger());
            var endPointB = new TcpEndPoint(localAddressResolver, BuildLogger());

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            _ = await endPointB.ReceiveAsync();

            Assert.IsTrue(endPointA.TryGetRemoteEndPoint(endPointB.LocalAddress, out var remoteEP));
            await remoteEP.Connection.DisposeAsync();

            message = BuildTestMessage();
            transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            var receivedTransmission = await endPointB.ReceiveAsync();

            Assert.AreEqual(endPointA.LocalAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }

        [TestMethod]
        public async Task ReceiverConnectionDisposeTest()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPointA = new TcpEndPoint(localAddressResolver, BuildLogger());
            var endPointB = new TcpEndPoint(localAddressResolver, BuildLogger());

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            _ = await endPointB.ReceiveAsync();

            Assert.IsTrue(endPointB.TryGetRemoteEndPoint(endPointA.LocalAddress, out var remoteEP));
            await remoteEP.Connection.DisposeAsync();

            message = BuildTestMessage();
            transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            var receivedTransmission = await endPointB.ReceiveAsync();

            Assert.AreEqual(endPointA.LocalAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }

        [TestMethod]
        public async Task SenderConnectionBreakdownTest()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPointA = new TcpEndPoint(localAddressResolver, BuildLogger());
            var endPointB = new TcpEndPoint(localAddressResolver, BuildLogger());

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            _ = await endPointB.ReceiveAsync();

            Assert.IsTrue(endPointA.TryGetRemoteEndPoint(endPointB.LocalAddress, out var remoteEP));
            remoteEP.Connection.Stream.Close();

            message = BuildTestMessage();
            transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            var receivedTransmission = await endPointB.ReceiveAsync();

            Assert.AreEqual(endPointA.LocalAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }

        [TestMethod]
        public async Task ReceiverConnectionBreakdownTest()
        {
            var localAddressResolver = BuildLocalAddressResolver();
            var endPointA = new TcpEndPoint(localAddressResolver, BuildLogger());
            var endPointB = new TcpEndPoint(localAddressResolver, BuildLogger());

            var message = BuildTestMessage();
            var transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            _ = await endPointB.ReceiveAsync();

            Assert.IsTrue(endPointB.TryGetRemoteEndPoint(endPointA.LocalAddress, out var remoteEP));
            remoteEP.Connection.Stream.Close();

            message = BuildTestMessage();
            transmission = new Transmission<IPEndPoint>(message, endPointB.LocalAddress);

            await endPointA.SendAsync(transmission);
            var receivedTransmission = await endPointB.ReceiveAsync();

            Assert.AreEqual(endPointA.LocalAddress, receivedTransmission.RemoteAddress);

            TestTestMessage(receivedTransmission.Message);
        }

        private static LocalAddressResolverMock BuildLocalAddressResolver()
        {
            return new LocalAddressResolverMock
            {
                LocalAddress = Dns
                                 .GetHostEntry(Dns.GetHostName())
                                 .AddressList
                                 .FirstOrDefault(p => p.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? IPAddress.Loopback
            };
        }

        private static ILogger<TcpEndPoint> BuildLogger()
        {
            var services = new ServiceCollection();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddDebug();
            });

            var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetRequiredService<ILogger<TcpEndPoint>>();
        }

        private ValueMessage BuildTestMessage()
        {
            var messageBuilder = new ValueMessageBuilder();

            using (var frameStream = messageBuilder.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write(4);
            }

            using (var frameStream = messageBuilder.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write(5);
            }

            return messageBuilder.BuildMessage();
        }

        private void TestTestMessage(ValueMessage message)
        {
            message = message.PopFrame(out var frame);

            using (var frameStream = frame.OpenStream())
            using (var reader = new BinaryReader(frameStream))
            {
                Assert.AreEqual(5, reader.ReadInt32());
            }

            message = message.PopFrame(out frame);

            using (var frameStream = frame.OpenStream())
            using (var reader = new BinaryReader(frameStream))
            {
                Assert.AreEqual(4, reader.ReadInt32());
            }
        }
    }
}
