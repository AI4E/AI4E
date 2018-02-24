/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Threading.Tasks;
using System.Threading;

#if DEBUG
using System.Diagnostics;
#endif

namespace AI4E.Async
{
    public static class TaskExtension
    {
        public static bool IsRunning(this Task task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            return !(task.IsCanceled || task.IsCompleted || task.IsFaulted);
        }

        public static void HandleExceptions(this Task task) // TODO: Receive an instance of ILogger type
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
#if DEBUG
                    Debugger.Break();
#endif

                    Console.WriteLine(t.Exception.ToString());
                    Console.WriteLine();
                }
            });
        }

        public static async Task WithCancellation(this Task task, CancellationToken cancellation)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (cancellation == default)
            {
                await task;
                return;
            }

            var completed = await Task.WhenAny(cancellation.AsTask(), task);

            if (completed != task)
            {
                task.HandleExceptions();
                throw new TaskCanceledException();
            }

            await task;
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellation)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (cancellation == default)
                return await task;

            var completed = await Task.WhenAny(cancellation.AsTask(), task);

            if (completed != task)
            {
                task.HandleExceptions();
                throw new TaskCanceledException();
            }

            return await task;
        }
    }
}
