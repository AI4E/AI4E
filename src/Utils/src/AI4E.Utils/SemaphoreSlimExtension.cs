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

using System.Diagnostics;
using System.Threading.Tasks;

namespace System.Threading
{
    public static class AI4EUtilsSemaphoreSlimExtension
    {
        // True if the lock could be taken immediately, false otherwise.
        public static ValueTask<bool> LockOrWaitAsync(this SemaphoreSlim semaphore, CancellationToken cancellation)
        {
#pragma warning disable CA1062
            if (semaphore.Wait(0))
#pragma warning restore CA1062
            {
                Debug.Assert(semaphore.CurrentCount == 0);
                return new ValueTask<bool>(true);
            }

            return WaitAsync(semaphore, cancellation);
        }

        private static async ValueTask<bool> WaitAsync(SemaphoreSlim semaphore, CancellationToken cancellation)
        {
            await semaphore.WaitAsync(cancellation).ConfigureAwait(false);
            Debug.Assert(semaphore.CurrentCount == 0);

            return false;
        }
    }
}
