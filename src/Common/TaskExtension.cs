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
using Microsoft.Extensions.Logging;
using System.Diagnostics;


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
                        logger.LogError(t.Exception.InnerException, "An exception occured in the task.");
                    }
                    else
                    {
                        Debug.WriteLine("An exception occured in the task.");
                        Debug.WriteLine(t.Exception.InnerException.ToString());
                    }
                }
            });
        }

        public static void HandleExceptions(this Task task)
        {
            HandleExceptions(task, logger: null);
        }

        public static async Task HandleExceptionsAsync(this Task task, ILogger logger)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            try
            {
                await task;
            }
            catch (Exception exc)
            {
                if (logger != null)
                {
                    logger.LogError(exc, "An exception occured in the task.");
                }
                else
                {
                    Debug.WriteLine("An exception occured in the task.");
                    Debug.WriteLine(exc.ToString());
                }
            }
        }

        public static Task HandleExceptionsAsync(this Task task)
        {
            return HandleExceptionsAsync(task, logger: null);
        }

        public static async Task<T> HandleExceptionsAsync<T>(this Task<T> task, ILogger logger, T placeholder)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            var result = placeholder;

            try
            {
                result = await task;
            }
            catch (Exception exc)
            {
                if (logger != null)
                {
                    logger.LogError(exc, "An exception occured in the task.");
                }
                else
                {
                    Debug.WriteLine("An exception occured in the task.");
                    Debug.WriteLine(exc.ToString());
                }
            }

            return result;
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task, T placeholder)
        {
            return HandleExceptionsAsync(task, logger: default, placeholder);
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task, ILogger logger)
        {
            return HandleExceptionsAsync(task, logger, placeholder: default);
        }

        public static Task<T> HandleExceptionsAsync<T>(this Task<T> task)
        {
            return HandleExceptionsAsync(task, logger: null, placeholder: default);
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

        public static object GetResult(this Task t)
        {
            t.Wait();

            if (t.IsFaulted || t.IsCanceled || !t.GetType().IsGenericType || t.GetType().GetGenericArguments()[0] == Type.GetType("System.Threading.Tasks.VoidTaskResult"))
            {
                return null;
            }

            return t.GetType().GetProperty("Result").GetValue(t);
        }
    }
}
