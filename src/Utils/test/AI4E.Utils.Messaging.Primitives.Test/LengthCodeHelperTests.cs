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
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Messaging.Primitives
{
    [TestClass]
    public sealed class LengthCodeHelperTests
    {
        [DataTestMethod]
        [DataRow(0x00_00_00_00, 1)]
        [DataRow(0x00_00_00_7F, 1)]
        [DataRow(0x00_00_00_80, 2)]
        [DataRow(0x00_00_3F_FF, 2)]
        [DataRow(0x00_00_40_00, 3)]
        [DataRow(0x00_1F_FF_FF, 3)]
        [DataRow(0x00_20_00_00, 4)]
        [DataRow(0x0F_FF_FF_FF, 4)]
        [DataRow(0x10_00_00_00, 5)]
        [DataRow(-1, 5)]
        public void Get7BitEndodedIntBytesCountTest(int value, int byteCount)
        {
            var r = LengthCodeHelper.Get7BitEndodedIntBytesCount(value);

            Assert.AreEqual(byteCount, r);
        }

        [DataTestMethod]
        [DataRow(0x00_00_00_00, 0x0, 1)]
        [DataRow(0x00_00_00_7F, 0x7F, 1)]
        [DataRow(0x00_00_00_80, 0x1_80, 2)]
        [DataRow(0x00_00_3F_FF, 0x7F_FF, 2)]
        [DataRow(0x00_00_40_00, 0x1_80_80, 3)]
        [DataRow(0x00_1F_FF_FF, 0x7F_FF_FF, 3)]
        [DataRow(0x00_20_00_00, 0x1_80_80_80, 4)]
        [DataRow(0x0F_FF_FF_FF, 0x7F_FF_FF_FF, 4)]
        [DataRow(0x10_00_00_00, 0x1_80_80_80_80, 5)]
        [DataRow(-1, 0xF_FF_FF_FF_FF, 5)]
        public void Write7BitEncodedIntTest(int value, long encodedValue, int byteCount)
        {
            Span<byte> buffer = stackalloc byte[8];
            LengthCodeHelper.Write7BitEncodedInt(buffer, value, out var r);

            Assert.AreEqual(byteCount, r);
            Assert.AreEqual(encodedValue, BinaryPrimitives.ReadInt64LittleEndian(buffer));
        }

        [DataTestMethod]
        [DataRow(0x00_00_00_00, 0x0, 1)]
        [DataRow(0x00_00_00_7F, 0x7F, 1)]
        [DataRow(0x00_00_00_80, 0x1_80, 2)]
        [DataRow(0x00_00_3F_FF, 0x7F_FF, 2)]
        [DataRow(0x00_00_40_00, 0x1_80_80, 3)]
        [DataRow(0x00_1F_FF_FF, 0x7F_FF_FF, 3)]
        [DataRow(0x00_20_00_00, 0x1_80_80_80, 4)]
        [DataRow(0x0F_FF_FF_FF, 0x7F_FF_FF_FF, 4)]
        [DataRow(0x10_00_00_00, 0x1_80_80_80_80, 5)]
        [DataRow(-1, 0xF_FF_FF_FF_FF, 5)]
        public void Write7BitEncodedInt2Test(int value, long encodedValue, int byteCount)
        {
            Span<byte> buffer = stackalloc byte[8];
            LengthCodeHelper.Write7BitEncodedInt(buffer, value);

            Assert.AreEqual(encodedValue, BinaryPrimitives.ReadInt64LittleEndian(buffer));
        }

        [DataTestMethod]
        [DataRow(0x00_00_00_00, 0x0, 1)]
        [DataRow(0x00_00_00_7F, 0x7F, 1)]
        [DataRow(0x00_00_00_80, 0x1_80, 2)]
        [DataRow(0x00_00_3F_FF, 0x7F_FF, 2)]
        [DataRow(0x00_00_40_00, 0x1_80_80, 3)]
        [DataRow(0x00_1F_FF_FF, 0x7F_FF_FF, 3)]
        [DataRow(0x00_20_00_00, 0x1_80_80_80, 4)]
        [DataRow(0x0F_FF_FF_FF, 0x7F_FF_FF_FF, 4)]
        [DataRow(0x10_00_00_00, 0x1_80_80_80_80, 5)]
        [DataRow(-1, 0xF_FF_FF_FF_FF, 5)]
        public void Read7BitEncodedIntTest(int value, long encodedValue, int byteCount)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, encodedValue);
            var x = LengthCodeHelper.Read7BitEncodedInt(buffer, out var r);

            Assert.AreEqual(byteCount, r);
            Assert.AreEqual(value, x);
        }

        [DataTestMethod]
        [DataRow(0x00_00_00_00, 0x0, 1)]
        [DataRow(0x00_00_00_7F, 0x7F, 1)]
        [DataRow(0x00_00_00_80, 0x1_80, 2)]
        [DataRow(0x00_00_3F_FF, 0x7F_FF, 2)]
        [DataRow(0x00_00_40_00, 0x1_80_80, 3)]
        [DataRow(0x00_1F_FF_FF, 0x7F_FF_FF, 3)]
        [DataRow(0x00_20_00_00, 0x1_80_80_80, 4)]
        [DataRow(0x0F_FF_FF_FF, 0x7F_FF_FF_FF, 4)]
        [DataRow(0x10_00_00_00, 0x1_80_80_80_80, 5)]
        [DataRow(-1, 0xF_FF_FF_FF_FF, 5)]
        public void Read7BitEncodedInt2Test(int value, long encodedValue, int byteCount)
        {
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, encodedValue);
            var x = LengthCodeHelper.Read7BitEncodedInt(buffer);

            Assert.AreEqual(value, x);
        }

        [DataTestMethod]
        [DataRow(0x00_00_00_00, 0x0, 1)]
        [DataRow(0x00_00_00_7F, 0x7F, 1)]
        [DataRow(0x00_00_00_80, 0x1_80, 2)]
        [DataRow(0x00_00_3F_FF, 0x7F_FF, 2)]
        [DataRow(0x00_00_40_00, 0x1_80_80, 3)]
        [DataRow(0x00_1F_FF_FF, 0x7F_FF_FF, 3)]
        [DataRow(0x00_20_00_00, 0x1_80_80_80, 4)]
        [DataRow(0x0F_FF_FF_FF, 0x7F_FF_FF_FF, 4)]
        [DataRow(0x10_00_00_00, 0x1_80_80_80_80, 5)]
        [DataRow(-1, 0xF_FF_FF_FF_FF, 5)]
        public async Task Read7BitEncodedIntFromStreamTest(int value, long encodedValue, int byteCount)
        {
            var buffer = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, encodedValue);
            using var stream = new MemoryStream();
            stream.Write(buffer);
            stream.Position = 0;

            var x = await LengthCodeHelper.Read7BitEncodedIntAsync(stream);

            Assert.AreEqual(byteCount, stream.Position);
            Assert.AreEqual(value, x);
        }

        [DataTestMethod]
        [DataRow(0x00_00_00_00, 0x0, 1)]
        [DataRow(0x00_00_00_7F, 0x7F, 1)]
        [DataRow(0x00_00_00_80, 0x1_80, 2)]
        [DataRow(0x00_00_3F_FF, 0x7F_FF, 2)]
        [DataRow(0x00_00_40_00, 0x1_80_80, 3)]
        [DataRow(0x00_1F_FF_FF, 0x7F_FF_FF, 3)]
        [DataRow(0x00_20_00_00, 0x1_80_80_80, 4)]
        [DataRow(0x0F_FF_FF_FF, 0x7F_FF_FF_FF, 4)]
        [DataRow(0x10_00_00_00, 0x1_80_80_80_80, 5)]
        [DataRow(-1, 0xF_FF_FF_FF_FF, 5)]
        public async Task Write7BitEncodedIntToStreamTest(int value, long encodedValue, int byteCount)
        {
            var buffer = new byte[8].AsMemory();
            using var stream = new MemoryStream();
            await LengthCodeHelper.Write7BitEncodedIntAsync(stream, value);
            Assert.AreEqual(byteCount, stream.Position);

            stream.Position = 0;
            await stream.ReadExactAsync(buffer[0..byteCount], default);

            Assert.AreEqual(encodedValue, BinaryPrimitives.ReadInt64LittleEndian(buffer.Span));
        }
    }
}
