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

using AI4E.Utils;

namespace System.Buffers
{
    public static class AI4EUtilsMemoryMemoryPoolExtensions
    {
        public static SlicedMemoryOwner<T> RentExact<T>(this MemoryPool<T> memoryPool, int length)
        {
#pragma warning disable CA1062
            var memoryOwner = memoryPool.Rent(length);
#pragma warning restore CA1062
            try
            {
                return new SlicedMemoryOwner<T>(memoryOwner, start: 0, length);
            }
            catch
            {
                memoryOwner.Dispose();
                throw;
            }
        }
    }
}
