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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace System
{
    public static class AI4EUtilsObjectExtension
    {
#pragma warning disable CA1720
        public static void DisposeIfDisposable(this object obj)
#pragma warning restore CA1720
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

#pragma warning disable CA1720
        public static ValueTask DisposeIfDisposableAsync(this object obj)
#pragma warning restore CA1720
        {
            if (obj is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return default;
        }

#pragma warning disable CA1720
        public static IEnumerable<object> Yield(this object obj)
#pragma warning restore CA1720
        {
            yield return obj;
        }

        public static IEnumerable<T> Yield<T>(this T t)
        {
            yield return t;
        }
    }
}
