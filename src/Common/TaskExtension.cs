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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AI4E.Internal
{
    internal static class TaskExtension
    {
        public static bool IsRunning(this Task task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            return !(task.IsCanceled || task.IsCompleted || task.IsFaulted);
        }

        #region IgnoreCancellation

        public static void IgnoreCancellation(this Task task, ILogger logger)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            task.ContinueWith(t =>
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
                            Debug.WriteLine(exception.ToString());
                        }
                    }
                }
            });
        }

        public static Task IgnoreCancellationAsync(this Task task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            var tcs = new TaskCompletionSource<object>();

            task.ContinueWith(t =>
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
            });

            return tcs.Task;
        }

        public static void IgnoreCancellation(this Task task)
        {
            IgnoreCancellation(task, logger: null);
        }

        #endregion

        public static void HandleExceptions(this Task task, ILogger logger)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    if (logger != null)
                    {
                        logger.LogError(t.Exception.InnerException, "An exception occured unexpectedly.");
                    }
                    else
                    {
                        Debug.WriteLine("An exception occured unexpectedly.");
                        Debug.WriteLine(t.Exception.InnerException.ToString());
                    }
                }
            });
        }

        public static void HandleExceptions(this Task task)
        {
            HandleExceptions(task, logger: null);
        }

        public static Task HandleExceptionsAsync(this Task task, ILogger logger)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            return ExceptionHelper.HandleExceptions(async () => await task, logger, Task.CompletedTask);
        }

        public static Task HandleExceptionsAsync(this Task task)
        {
            return HandleExceptionsAsync(task, logger: null);
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task, ILogger logger, T defaultValue)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            return ExceptionHelper.HandleExceptions(async () => await task, logger, Task.FromResult(defaultValue));
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task, T defaultValue)
        {
            return HandleExceptionsAsync(task, logger: default, defaultValue);
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task, ILogger logger)
        {
            return HandleExceptionsAsync(task, logger, defaultValue: default);
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task)
        {
            return HandleExceptionsAsync(task, logger: null, defaultValue: default);
        }

        public static Task WithCancellation(this Task task, CancellationToken cancellation)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (!cancellation.CanBeCanceled || task.IsCompleted)
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

            using (cancellation.Register(() => tcs.TrySetCanceled(cancellation), useSynchronizationContext: false))
            {
                await await Task.WhenAny(tcs.Task, task).ConfigureAwait(false);
            }
        }

        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellation)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (!cancellation.CanBeCanceled || task.IsCompleted)
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

            using (cancellation.Register(() => tcs.TrySetCanceled(cancellation), useSynchronizationContext: false))
            {
                return await await Task.WhenAny(tcs.Task, task).ConfigureAwait(false);
            }
        }

        public static object GetResult(this Task t)
        {
            t.Wait();

            if (t.IsFaulted || t.IsCanceled || !t.GetType().IsGenericType || t.GetType().GetGenericArguments()[0] == Type.GetType("System.Threading.Tasks.VoidTaskResult"))
            {
                return null;
            }

            return t.GetType().GetProperty("Result").GetValue(t);
        }

        public static Task<T> WithResult<T>(this Task t, T result)
        {
            if (t == null)
                throw new ArgumentNullException(nameof(t));

            return t.ContinueWith(_ => result);
        }
    }

    internal static class ExceptionHelper
    {
        public static void HandleExceptions(Action action, ILogger logger)
        {
            try
            {
                action();
            }
            catch (Exception exc)
            {
                if (logger != null)
                {
                    logger.LogError(exc, "An exception occured unexpectedly.");
                }
                else
                {
                    Debug.WriteLine("An exception occured unexpectedly.");
                    Debug.WriteLine(exc.ToString());
                }
            }
        }

        public static T HandleExceptions<T>(Func<T> func, ILogger logger, T defaultValue = default)
        {
            try
            {
                return func();
            }
            catch (Exception exc)
            {
                if (logger != null)
                {
                    logger.LogError(exc, "An exception occured unexpectedly");
                }
                else
                {
                    Debug.WriteLine("An exception occured unexpectedly.");
                    Debug.WriteLine(exc.ToString());
                }
            }

            return defaultValue;
        }
    }
}
