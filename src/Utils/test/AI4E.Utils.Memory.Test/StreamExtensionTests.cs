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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Memory
{
    [TestClass]
    public sealed class StreamExtensionTests
    {
        [TestMethod]
        public void DataAvailableAsyncTest()
        {
            var inBuffer = Enumerable.Range(0, 10).Select(p => (byte)p).ToArray();
            var outBuffer = new byte[10];
            var stream = new MemoryStream();

            stream.Write(inBuffer, 0, inBuffer.Length);
            stream.Position = 0;

            var task = stream.ReadExactAsync(outBuffer, cancellation: default).AsTask();

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
            Assert.IsTrue(inBuffer.SequenceEqual(outBuffer));
        }

        [TestMethod]
        public async Task DataNotAvailableAsyncTest()
        {
            var outBuffer = new byte[10];
            var stream = new MemoryStream();

            await Assert.ThrowsExceptionAsync<EndOfStreamException>(async () =>
            {
                await stream.ReadExactAsync(outBuffer, cancellation: default);
            });
        }

        [TestMethod]
        public async Task ThrowsOnNullStreamAsyncTest()
        {
            var outBuffer = new byte[10];
            Stream stream = null;

            await Assert.ThrowsExceptionAsync<NullReferenceException>(async () =>
            {
                await stream.ReadExactAsync(outBuffer, cancellation: default);
            });
        }

        [TestMethod]
        public void DataAvailableTest()
        {
            var inBuffer = Enumerable.Range(0, 10).Select(p => (byte)p).ToArray();
            var outBuffer = new byte[10];
            var stream = new MemoryStream();

            stream.Write(inBuffer, 0, inBuffer.Length);
            stream.Position = 0;

            stream.ReadExact(outBuffer.AsSpan());

            Assert.IsTrue(inBuffer.SequenceEqual(outBuffer));
        }

        [TestMethod]
        public void DataNotAvailableTest()
        {
            var outBuffer = new byte[10];
            var stream = new MemoryStream();

            Assert.ThrowsException<EndOfStreamException>(() =>
            {
                stream.ReadExact(outBuffer);
            });
        }

        [TestMethod]
        public void ThrowsOnNullStreamTest()
        {
            var outBuffer = new byte[10];
            Stream stream = null;

            Assert.ThrowsException<NullReferenceException>(() =>
            {
                stream.ReadExact(outBuffer);
            });
        }
    }
}
