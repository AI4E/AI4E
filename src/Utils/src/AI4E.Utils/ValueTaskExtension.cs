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
using System.Diagnostics;
using System.Linq;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;

namespace System.Threading.Tasks
{
    public static class AI4EUtilsValueTaskExtension
    {
        public static void HandleExceptions(this ValueTask valueTask, ILogger? logger = null)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                return;
            }

            if (valueTask.IsCompleted)
            {
                try
                {
                    valueTask.GetAwaiter().GetResult();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    ExceptionHelper.LogException(exc, logger);
                }

                return;
            }

            valueTask.AsTask().HandleExceptions(logger);
        }

        public static void HandleExceptions<T>(this ValueTask<T> valueTask, ILogger? logger = null)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                return;
            }

            if (valueTask.IsCompleted)
            {
                try
                {
                    valueTask.GetAwaiter().GetResult();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    ExceptionHelper.LogException(exc, logger);
                }

                return;
            }

            valueTask.AsTask().HandleExceptions(logger);
        }

        public static ValueTask HandleExceptionsAsync(this ValueTask valueTask, ILogger? logger = null)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                return default;
            }

            if (valueTask.IsCompleted)
            {
                try
                {
                    valueTask.GetAwaiter().GetResult();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    ExceptionHelper.LogException(exc, logger);
                }

                return default;
            }

            return valueTask.AsTask().HandleExceptionsAsync(logger).AsValueTask();
        }

        public static ValueTask<T> HandleExceptionsAsync<T>(this ValueTask<T> valueTask, T defaultValue = default, ILogger? logger = null)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                return valueTask;
            }

            if (valueTask.IsCompleted)
            {
                try
                {
                    return new ValueTask<T>(valueTask.GetAwaiter().GetResult());
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    ExceptionHelper.LogException(exc, logger);
                }
            }

            return valueTask.AsTask().HandleExceptionsAsync(defaultValue, logger).AsValueTask();
        }

        public static ValueTask WithCancellation(this ValueTask task, CancellationToken cancellation)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (!cancellation.CanBeCanceled || task.IsCompleted)
            {
                return task;
            }

            if (cancellation.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellation).AsValueTask();
            }

            return InternalWithCancellation(task, cancellation);
        }

        private static async ValueTask InternalWithCancellation(ValueTask task, CancellationToken cancellation)
        {
            var tcs = ValueTaskCompletionSource.Create();

            static async void Execute(ValueTaskCompletionSource tcs, ValueTask task)
            {
                try
                {
                    await task;
                    tcs.TrySetResult();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    tcs.TrySetExceptionOrCanceled(exc);
                }
            }

            Execute(tcs, task);

            using (cancellation.Register(() => tcs.TrySetCanceled(cancellation), useSynchronizationContext: false))
            {
                await tcs.Task;
            }
        }

        public static ValueTask<T> WithCancellation<T>(this ValueTask<T> task, CancellationToken cancellation)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (!cancellation.CanBeCanceled || task.IsCompleted)
            {
                return task;
            }

            if (cancellation.IsCancellationRequested)
            {
                return Task.FromCanceled<T>(cancellation).AsValueTask();
            }

            return InternalWithCancellation(task, cancellation);
        }

        private static async ValueTask<T> InternalWithCancellation<T>(ValueTask<T> task, CancellationToken cancellation)
        {
            var tcs = ValueTaskCompletionSource<T>.Create();

            static async void Execute(ValueTaskCompletionSource<T> tcs, ValueTask<T> task)
            {
                try
                {
                    tcs.TrySetResult(await task);
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    tcs.TrySetExceptionOrCanceled(exc);
                }
            }

            Execute(tcs, task);

            using (cancellation.Register(() => tcs.TrySetCanceled(cancellation), useSynchronizationContext: false))
            {
                return await tcs.Task;
            }
        }

        /// <summary>
        /// Creates a value-task that completes when all of the value-tasks in the source enumerable completed.
        /// </summary>
        /// <typeparam name="T">The type of the completed value-task.</typeparam>
        /// <param name="tasks">The enumerable of value-tasks to wait on for completion.</param>
        /// <param name="preserveOrder">A boolean value indicating whether the order of the tasks shall be preserved.</param>
        /// <returns>A value-task that represents the completion of all of the supplied value-tasks.</returns>
        public static ValueTask<IEnumerable<T>> WhenAll<T>(this IEnumerable<ValueTask<T>> tasks, bool preserveOrder = true)
        {
            // We do not capture tasks to prevent allocation for the captured data.
            static ValueTask<IEnumerable<T>> PreservedOrder(IEnumerable<ValueTask<T>> valueTasks)
            {
                List<T> result;
                List<Task> tasksToAwait;

                var wasCanceled = false;
                List<Exception>? exceptions = null;

                if (valueTasks is ICollection<ValueTask<T>> collection)
                {
                    result = new List<T>(capacity: collection.Count);
                    tasksToAwait = new List<Task>(capacity: collection.Count);
                }
                else if (valueTasks is IReadOnlyCollection<ValueTask<T>> readOnlyCollection)
                {
                    result = new List<T>(capacity: readOnlyCollection.Count);
                    tasksToAwait = new List<Task>(capacity: readOnlyCollection.Count);
                }
                else
                {
                    result = new List<T>();
                    tasksToAwait = new List<Task>();
                }

                var i = 0;

#pragma warning disable CA1062
                foreach (var valueTask in valueTasks)
#pragma warning restore CA1062
                {
                    if (valueTask.IsCompletedSuccessfully)
                    {
                        lock (result)
                        {
                            Debug.Assert(result.Count == i);
                            result.Add(valueTask.Result);
                        }
                    }
                    else
                    {
                        lock (result)
                        {
#pragma warning disable CS8653
                            // We add a default (possibly null value) for now, but we ensure that we replace this with a
                            // legal value later.
                            result.Add(default!);
#pragma warning restore CS8653
                        }

                        var task = valueTask.AsTask();
                        var index = i; // This is copied to a new variable in order
                                       // to capture the current value and not a future one.

                        var taskToAwait = task.ContinueWith(t =>
                        {
                            Debug.Assert(t.IsCompleted);

                            if (t.IsFaulted)
                            {
                                Debug.Assert(t.Exception != null);

                                var exceptionList = Volatile.Read(ref exceptions);

                                if (exceptionList == null)
                                {
                                    exceptionList = new List<Exception>();
                                    exceptionList = Interlocked.CompareExchange(ref exceptions, exceptionList, null)
                                    ?? exceptionList;
                                }

                                // TODO: Unwrap the exception
                                var exception = t.Exception!.InnerException ?? t.Exception;
                                exceptionList.Add(exception);
                            }
                            else if (t.IsCanceled)
                            {
                                Volatile.Write(ref wasCanceled, true);
                            }
                            else
                            {
                                lock (result)
                                {
                                    result[index] = t.Result;
                                }
                            }
                        }, TaskScheduler.Default);

                        tasksToAwait.Add(taskToAwait);
                    }

                    i++;
                }

                if (tasksToAwait.Count == 0)
                {
                    return new ValueTask<IEnumerable<T>>(result);
                }

                var taskCompletionSource = ValueTaskCompletionSource<IEnumerable<T>>.Create();

                Task.WhenAll(tasksToAwait).ContinueWith(t =>
                {
                    if (exceptions != null)
                    {
                        taskCompletionSource.TrySetException(exceptions);
                    }
                    else if (wasCanceled)
                    {
                        taskCompletionSource.TrySetCanceled();
                    }
                    else
                    {
                        taskCompletionSource.TrySetResult(result);
                    }
                }, TaskScheduler.Default);

                return taskCompletionSource.Task;
            }

            // We do not capture tasks to prevent allocation for the captured data.
            static async ValueTask<IEnumerable<T>> NotPreservedOrder(IEnumerable<ValueTask<T>> valueTasks)
            {
                List<T> result;
                List<Task<T>> tasksToAwait;

                if (valueTasks is ICollection<ValueTask<T>> collection)
                {
                    result = new List<T>(capacity: collection.Count);
                    tasksToAwait = new List<Task<T>>(capacity: collection.Count);
                }
                else if (valueTasks is IReadOnlyCollection<ValueTask<T>> readOnlyCollection)
                {
                    result = new List<T>(capacity: readOnlyCollection.Count);
                    tasksToAwait = new List<Task<T>>(capacity: readOnlyCollection.Count);
                }
                else
                {
                    result = new List<T>();
                    tasksToAwait = new List<Task<T>>();
                }

                foreach (var valueTask in valueTasks)
                {
                    if (valueTask.IsCompletedSuccessfully)
                    {
                        result.Add(valueTask.Result);
                    }
                    else
                    {
                        tasksToAwait.Add(valueTask.AsTask());
                    }
                }

                result.AddRange(await Task.WhenAll(tasksToAwait).ConfigureAwait(false));

                return result;
            }

            if (preserveOrder)
            {
                return PreservedOrder(tasks);
            }

            return NotPreservedOrder(tasks);
        }

        /// <summary>
        /// Creates a value-task that completes when all of the value-tasks in the source enumerable completed.
        /// </summary>
        /// <param name="tasks">The enumerable of value-tasks to wait on for completion.</param>
        /// <returns>A value-task that represents the completion of all of the supplied value-tasks.</returns>
        public static ValueTask WhenAll(this IEnumerable<ValueTask> tasks)
        {
            List<Task>? tasksToAwait = null;

            if (tasks is ICollection<ValueTask> collection)
            {
                tasksToAwait = new List<Task>(capacity: collection.Count);
            }
            else if (tasks is IReadOnlyCollection<ValueTask> readOnlyCollection)
            {
                tasksToAwait = new List<Task>(capacity: readOnlyCollection.Count);
            }

#pragma warning disable CA1062
            foreach (var valueTask in tasks)
#pragma warning restore CA1062
            {
                if (!valueTask.IsCompletedSuccessfully)
                {
                    if (tasksToAwait == null)
                    {
                        tasksToAwait = new List<Task>();
                    }

                    tasksToAwait.Add(valueTask.AsTask());
                }
            }

            if (tasksToAwait == null || !tasksToAwait.Any())
            {
                return default;
            }

            return new ValueTask(Task.WhenAll(tasksToAwait));
        }

        public static async IAsyncEnumerable<T> YieldAsync<T>(this ValueTask<T> task)
        {
            yield return await task;
        }
    }
}
