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

using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;

namespace System.Diagnostics
{
    public static class AI4EUtilsProcessExtension
    {
        public static ValueTask WaitForExitAsync(this Process process, CancellationToken cancellation = default)
        {
            var tcs = ValueTaskCompletionSource.Create();

#pragma warning disable CA1062
            process.EnableRaisingEvents = true;
#pragma warning restore CA1062
            process.Exited += (s, o) => tcs.TrySetResult();

            // This is needed in order to prevent a race condition when the process exits before we can setup our event handler.
            process.Refresh();
            if (process.HasExited)
            {
                tcs.TrySetResult();
            }

            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => tcs.TrySetCanceled());
            }

            return tcs.Task;
        }
    }
}
