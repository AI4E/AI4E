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
            SlicedMemoryOwner<byte> symbolsBytesOwner)
        {
            _assemblyBytesOwner = assemblyBytesOwner;
            _symbolsBytesOwner = symbolsBytesOwner;
            HasSymbols = true;
        }

        public BlazorModuleAssemblySource(SlicedMemoryOwner<byte> assemblyBytesOwner)
        {
            _assemblyBytesOwner = assemblyBytesOwner;
            _symbolsBytesOwner = default;
            HasSymbols = false;
        }

        public ReadOnlyMemory<byte> AssemblyBytes => _assemblyBytesOwner.Memory;
        public ReadOnlyMemory<byte> SymbolsBytes => _symbolsBytesOwner.Memory;

        public bool HasSymbols { get; }

        public void Dispose()
        {
            try
            {
                _assemblyBytesOwner.Dispose();
            }
            finally
            {
                if (HasSymbols)
                {
                    _symbolsBytesOwner.Dispose(); // TODO: If this throws, the original exception gets lost.
                }
            }
        }
    }
}
