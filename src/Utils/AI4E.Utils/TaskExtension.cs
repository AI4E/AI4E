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
using System.Reflection;
using AI4E.Utils;
using Microsoft.Extensions.Logging;

namespace System.Threading.Tasks
{
    public static class AI4EUtilsTaskExtension
    {
        public static bool IsRunning(this Task task)
        {
#pragma warning disable CA1062
            return !(task.IsCanceled || task.IsCompleted || task.IsFaulted);
#pragma warning restore CA1062
        }

        public static void IgnoreCancellation(this Task task, ILogger? logger = null)
        {
#pragma warning disable CA1062
            task.ContinueWith(t =>
#pragma warning restore CA1062
            {
                if (t.Exception != null)
                {
                    var exception = t.Exception.InnerException;

                    if (!(exception is OperationCanceledException))
                    {
                        if (logger != null)
                        {
                            logger.LogError(exception, "An exception occured in the task.");
                        }
                        else
                        {
                            Debug.WriteLine("An exception occured in the task.");
                            if (exception != null)
                            {
                                Debug.WriteLine(exception.ToString());
                            }
                        }
                    }
                }
            }, TaskScheduler.Default);
        }

        public static Task IgnoreCancellationAsync(this Task task)
        {
            var tcs = new TaskCompletionSource<object?>();
#pragma warning disable CA1062
            task.ContinueWith(t =>
#pragma warning restore CA1062
            {
                if (t.Exception != null &&
                    t.Exception.InnerExceptions.Any(e => !(e is OperationCanceledException)))
                {
                    tcs.SetException(t.Exception);
                }
                else
                {
                    tcs.SetResult(null);
                }
            }, TaskScheduler.Default);

            return tcs.Task;
        }

        public static void IgnoreCancellation(this Task task)
        {
            IgnoreCancellation(task, logger: null);
        }

        public static void HandleExceptions(this Task task, ILogger? logger = null)
        {
#pragma warning disable CA1062
            task.ContinueWith(t =>
#pragma warning restore CA1062
            {
                if (t.Exception != null)
                {
                    if (logger != null)
                    {
                        logger.LogError(t.Exception.InnerException, "An exception occured unexpectedly.");
                    }
                    else
                    {
                        Debug.WriteLine("An exception occured in the task.");
                        Debug.WriteLine(t.Exception.ToString());
                    }
                }
            }, TaskScheduler.Default);
        }

        public static Task HandleExceptionsAsync(this Task task, ILogger? logger = null)
        {
            return ExceptionHelper.HandleExceptions(async () => await task.ConfigureAwait(false), logger, Task.CompletedTask);
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task, T defaultValue = default, ILogger? logger = null)
        {
            return ExceptionHelper.HandleExceptions(async () => await task.ConfigureAwait(false), logger, Task.FromResult(defaultValue));
        }

        public static Task WithCancellation(this Task task, CancellationToken cancellation)
        {
#pragma warning disable CA1062
            if (!cancellation.CanBeCanceled || task.IsCompleted)
#pragma warning restore CA1062
            {
                return task;
            }

            if (cancellation.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellation);
            }

            return InternalWithCancellation(task, cancellation);
        }

        private static async Task InternalWithCancellation(Task task, CancellationToken cancellation)
        {
            var tcs = new TaskCompletionSource<object>();
            var cancellationTask = tcs.Task;

            using (cancellation.Register(() => tcs.TrySetCanceled(cancellation), useSynchronizationContext: false))
            {
                var completed = await Task.WhenAny(tcs.Task, task).ConfigureAwait(false);

                if (completed == cancellationTask)
                {
                    Debug.Assert(cancellation.IsCancellationRequested);

                    task.HandleExceptions();
                }

                await completed.ConfigureAwait(false);
            }
        }

        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellation)
        {
#pragma warning disable CA1062
            if (!cancellation.CanBeCanceled || task.IsCompleted)
#pragma warning restore CA1062
            {
                return task;
            }

            if (cancellation.IsCancellationRequested)
            {
                return Task.FromCanceled<T>(cancellation);
            }

            return InternalWithCancellation(task, cancellation);
        }

        private static async Task<T> InternalWithCancellation<T>(Task<T> task, CancellationToken cancellation)
        {
            var tcs = new TaskCompletionSource<T>();
            var cancellationTask = tcs.Task;

            using (cancellation.Register(() => tcs.TrySetCanceled(cancellation), useSynchronizationContext: false))
            {
                var completed = await Task.WhenAny(tcs.Task, task).ConfigureAwait(false);

                if (completed == cancellationTask)
                {
                    Debug.Assert(cancellation.IsCancellationRequested);

                    task.HandleExceptions();
                }

                return await completed.ConfigureAwait(false);
            }
        }

        public static object? GetResultOrDefault(this Task task)
        {
#pragma warning disable CS8625
            // Just ignore the nullability warning here as the value is never used in the target method.
            return GetResultOrDefault(task, null);
#pragma warning restore CS8625
        }

        public static object GetResultOrDefault(this Task task, object defaultValue)
        {
#pragma warning disable CA1062
            task.ConfigureAwait(false).GetAwaiter().GetResult();
#pragma warning restore CA1062

            if (task.IsFaulted ||
                task.IsCanceled ||
                task.GetResultType() == typeof(void))
            {
                return defaultValue;
            }

            return task.GetType().GetProperty("Result")!.GetValue(task) ?? defaultValue;
        }

        public static Type GetResultType(this Task task)
        {
#pragma warning disable CA1062
            var taskType = task.GetType();
#pragma warning restore CA1062
            var result = taskType.GetTaskResultType();
            Debug.Assert(result != null);
            return result!;
        }

        public static async Task<T> WithResult<T>(this Task task, T result)
        {
#pragma warning disable CA1062
            await task.ConfigureAwait(false);
#pragma warning restore CA1062

            return result;
        }

        public static ValueTask AsValueTask(this Task task)
        {
            return new ValueTask(task);
        }

        public static ValueTask<T> AsValueTask<T>(this Task<T> task)
        {
            return new ValueTask<T>(task);
        }

        public static async IAsyncEnumerable<T> YieldAsync<T>(this Task<T> task)
        {
#pragma warning disable CA1062
            yield return await task.ConfigureAwait(false);
#pragma warning restore CA1062
        }

        private static readonly MethodInfo _convertTaskMethodDefinition =
            typeof(AI4EUtilsTaskExtension)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .SingleOrDefault(p => p.Name == nameof(ConvertTask) && p.IsGenericMethodDefinition)
            ?? throw new Exception($"Unable to reflect method 'AI4EUtilsTaskExtension.ConvertTask`2'.");

        // Converts Task<T> to Task<TResult> with TResult == resultType and T must be convertible to TResult.
        public static Task Convert(this Task task, Type resultType)
        {
            if (resultType is null)
                throw new ArgumentNullException(nameof(resultType));

            if (resultType == typeof(void))
                return task;

            var taskType = task.GetResultType();

            if (taskType == resultType)
                return task;

            if (taskType == typeof(void))
                throw new ArgumentException("The argument must be an instance of a generic task type.", nameof(task));

            try
            {
                var result = _convertTaskMethodDefinition
                    .MakeGenericMethod(taskType, resultType)
                    .Invoke(null, new[] { task }) as Task;
                Debug.Assert(result != null);
                return result!;
            }
            catch (TargetInvocationException exc)
            {
                if (exc.InnerException != null)
                {
                    throw exc.InnerException;
                }

                throw;
            }
        }

        private static async Task<TResult> ConvertTask<T, TResult>(Task<T> task)
        {
            var obj = await task.ConfigureAwait(false);

            if (obj is null)
                return default!;

            return (TResult)(object)obj;
        }
    }
}
