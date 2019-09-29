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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;
using AI4E.Routing;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public readonly struct DebugModuleProperties : IEquatable<DebugModuleProperties>
    {
        public DebugModuleProperties(EndPointAddress endPoint, ModuleIdentifier module, ModuleVersion version)
        {
            EndPoint = endPoint;
            Module = module;
            Version = version;
        }

        public EndPointAddress EndPoint { get; }
        public ModuleIdentifier Module { get; }
        public ModuleVersion Version { get; }

        public async Task WriteAsync(Stream stream, CancellationToken cancellation)
        {
            var bufferSize = 0;
            bufferSize += 4 + EndPoint.Utf8EncodedValue.Length; // endPoint
            bufferSize += 4 + Encoding.UTF8.GetByteCount(Module.Name); // module
            bufferSize += 4 + 4 + 4 + 1; // version

            using var memoryOwner = MemoryPool<byte>.Shared.RentExact(4 + bufferSize);
            var memory = memoryOwner.Memory;
            BinaryPrimitives.WriteInt32LittleEndian(memory.Span, bufferSize + 4);
            Encode(memory.Span.Slice(4));
            await stream.WriteAsync(memory, cancellation);
        }

        public static async Task<DebugModuleProperties> ReadAsync(Stream stream, CancellationToken cancellation)
        {
            int length;

            using (var memoryOwner = MemoryPool<byte>.Shared.RentExact(4))
            {
                var memory = memoryOwner.Memory;
                await stream.ReadExactAsync(memory, cancellation);
                length = BinaryPrimitives.ReadInt32LittleEndian(memory.Span) - 4;
            }


            using (var memoryOwner = MemoryPool<byte>.Shared.RentExact(length))
            {
                var memory = memoryOwner.Memory;
                await stream.ReadExactAsync(memory, cancellation);
                return Decode(memory.Span);
            }
        }

        private void Encode(Span<byte> memory)
        {
            var writer = new BinarySpanWriter(memory, ByteOrder.LittleEndian);
            writer.Write(EndPoint.Utf8EncodedValue.Span, lengthPrefix: true);
            writer.Write(Module.Name.AsSpan(), lengthPrefix: true);
            writer.WriteInt32(Version.MajorVersion);
            writer.WriteInt32(Version.MinorVersion);
            writer.WriteInt32(Version.Revision);
            writer.WriteBool(Version.IsPreRelease);

            Assert(memory.Length == writer.Length);
        }

        private static DebugModuleProperties Decode(ReadOnlySpan<byte> memory)
        {
            var reader = new BinarySpanReader(memory, ByteOrder.LittleEndian);
            var endPoint = new EndPointAddress(reader.Read().ToArray()); // TODO: This copies
            var module = new ModuleIdentifier(reader.ReadString());
            var major = reader.ReadInt32();
            var minor = reader.ReadInt32();
            var revision = reader.ReadInt32();
            var isPreRelease = reader.ReadBool();
            var version = new ModuleVersion(major, minor, revision, isPreRelease);

            return new DebugModuleProperties(endPoint, module, version);
        }

        public bool Equals(in DebugModuleProperties other)
        {
            return EndPoint == other.EndPoint &&
                    Module == other.Module &&
                    Version == other.Version;
        }

        bool IEquatable<DebugModuleProperties>.Equals(DebugModuleProperties other)
        {
            return Equals(other);
        }

        public override bool Equals(object obj)
        {
            return obj is DebugModuleProperties debugModuleProperties && Equals(debugModuleProperties);
        }

        public override int GetHashCode()
        {
            var hashCode = 248443512;
            hashCode = hashCode * -1521134295 + EqualityComparer<EndPointAddress>.Default.GetHashCode(EndPoint);
            hashCode = hashCode * -1521134295 + EqualityComparer<ModuleIdentifier>.Default.GetHashCode(Module);
            hashCode = hashCode * -1521134295 + EqualityComparer<ModuleVersion>.Default.GetHashCode(Version);
            return hashCode;
        }

        public static bool operator ==(in DebugModuleProperties left, in DebugModuleProperties right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in DebugModuleProperties left, in DebugModuleProperties right)
        {
            return !left.Equals(right);
        }
    }
}
