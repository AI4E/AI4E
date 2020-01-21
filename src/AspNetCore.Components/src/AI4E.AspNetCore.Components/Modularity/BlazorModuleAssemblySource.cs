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
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Modularity
{
#pragma warning disable CA1815
    public readonly struct BlazorModuleAssemblySource : IDisposable
#pragma warning restore CA1815
    {
        private readonly SlicedMemoryOwner<byte> _assemblyBytesOwner;
        private readonly SlicedMemoryOwner<byte> _symbolsBytesOwner;

        public BlazorModuleAssemblySource(
            SlicedMemoryOwner<byte> assemblyBytesOwner,
            SlicedMemoryOwner<byte> symbolsBytesOwner,
            bool forceLoad = false)
        {
            _assemblyBytesOwner = assemblyBytesOwner;
            _symbolsBytesOwner = symbolsBytesOwner;
            HasSymbols = true;
            ForceLoad = forceLoad;
        }

        public BlazorModuleAssemblySource(SlicedMemoryOwner<byte> assemblyBytesOwner, bool forceLoad = false)
        {
            _assemblyBytesOwner = assemblyBytesOwner;
            _symbolsBytesOwner = default;
            HasSymbols = false;
            ForceLoad = forceLoad;
        }

        public ReadOnlyMemory<byte> AssemblyBytes => _assemblyBytesOwner.Memory;
        public ReadOnlyMemory<byte> SymbolsBytes => _symbolsBytesOwner.Memory;

        public bool HasSymbols { get; }
        public bool ForceLoad { get; }

        public void Dispose()
        {
            if (!HasSymbols)
            {
                _assemblyBytesOwner.Dispose();
                return;
            }

            Exception? exception = null;

            try
            {
                _assemblyBytesOwner.Dispose();
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                exception = exc;
            }

            try
            {
                _symbolsBytesOwner.Dispose();
            }
            catch (Exception exc)
            {
                if (exception != null)
                {
#pragma warning disable CA1065
                    throw new AggregateException(exception, exc);
#pragma warning restore CA1065
                }
                else
                {
                    throw;
                }
            }

            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        public BlazorModuleAssemblySource Configure(bool forceLoad)
        {
            if (forceLoad == ForceLoad)
                return this;

            if (HasSymbols)
            {
                return new BlazorModuleAssemblySource(_assemblyBytesOwner, _symbolsBytesOwner, forceLoad);
            }

            return new BlazorModuleAssemblySource(_assemblyBytesOwner, forceLoad);
        }

        public static async ValueTask<BlazorModuleAssemblySource> FromLocationAsync(
            string assemblyLocation,
            bool forceLoad = false,
            CancellationToken cancellation = default)
        {
            using var assemblyStream = new FileStream(
                            assemblyLocation,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: 4096,
                            useAsync: true);

            Debug.Assert(assemblyStream.CanSeek);
            var assemblyBytesOwner = MemoryPool<byte>.Shared.RentExact(checked((int)assemblyStream.Length));

            try
            {
                await assemblyStream.ReadExactAsync(assemblyBytesOwner.Memory, cancellation);

                var symbolsLocation = Path.ChangeExtension(assemblyLocation, "pdb");

                if (!File.Exists(symbolsLocation))
                {
                    return new BlazorModuleAssemblySource(assemblyBytesOwner, forceLoad);
                }

                using var symbolsStream = new FileStream(
                    symbolsLocation,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                Debug.Assert(symbolsStream.CanSeek);
                var symbolsBytesOwner = MemoryPool<byte>.Shared.RentExact(checked((int)symbolsStream.Length));

                try
                {
                    await symbolsStream.ReadExactAsync(symbolsBytesOwner.Memory, cancellation);
                    return new BlazorModuleAssemblySource(assemblyBytesOwner, symbolsBytesOwner, forceLoad);
                }
                catch
                {
                    symbolsBytesOwner.Dispose();
                    throw;
                }
            }
            catch
            {
                assemblyBytesOwner.Dispose();
                throw;
            }
        }
    }
}
