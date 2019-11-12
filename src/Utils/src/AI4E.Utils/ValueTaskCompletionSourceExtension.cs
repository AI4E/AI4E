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
using System.Threading.Tasks;
using AI4E.Utils.Async;

namespace AI4E.Utils
{
    public static class ValueTaskCompletionSourceExtension
    {
        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask{TResult}"/> object
        /// into the <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/> state,
        /// depending on the type of exception.
        /// </summary>
        /// <typeparam name="TResult">
        /// The type of the result value associated with the <see cref="ValueTaskCompletionSource{TResult}"/>.
        /// </typeparam>
        /// <param name="taskCompletionSource">The task completion source.</param>
        /// <param name="exception">The exception to bind to the <see cref="ValueTask{TResult}"/>.</param>
        /// <returns>True if the operation was succesful, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="taskCompletionSource"/> or <paramref name="exception"/> is <c>null</c>.
        /// </exception>
        public static bool TrySetExceptionOrCanceled<TResult>(
            this ValueTaskCompletionSource<TResult> taskCompletionSource,
            Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (exception is OperationCanceledException operationCanceledException)
            {
                var cancellationToken = operationCanceledException.CancellationToken;

                if (cancellationToken != default)
                {
                    return taskCompletionSource.TrySetCanceled(cancellationToken);
                }

                return taskCompletionSource.TrySetCanceled();
            }

            return taskCompletionSource.TrySetException(exception);
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask{TResult}"/> object
        /// into the <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/> state,
        /// depending on the type of exception.
        /// </summary>
        /// <typeparam name="TResult">
        /// The type of the result value associated with the <see cref="ValueTaskCompletionSource{TResult}"/>.
        /// </typeparam>
        /// <param name="taskCompletionSource">The task completion source.</param>
        /// <param name="exception">The exception to bind to the <see cref="ValueTask{TResult}"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="taskCompletionSource"/> or <paramref name="exception"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="ValueTask{TResult}"/> is already in one of the three final states:
        /// <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/>,
        /// or <see cref="TaskStatus.Canceled"/>.
        /// </exception>
        public static void SetExceptionOrCanceled<TResult>(
            this ValueTaskCompletionSource<TResult> taskCompletionSource,
            Exception exception)
        {
            if (!TrySetExceptionOrCanceled(taskCompletionSource, exception))
            {
                throw new InvalidOperationException(
                    "The underlying Task<TResult> is already in one of the three final states:" +
                    " RanToCompletion, Faulted, or Canceled.");
            }
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask"/> object
        /// into the <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/> state,
        /// depending on the type of exception.
        /// </summary>
        /// <param name="taskCompletionSource">The task completion source.</param>
        /// <param name="exception">The exception to bind to the <see cref="ValueTask"/>.</param>
        /// <returns>True if the operation was succesful, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="taskCompletionSource"/> or <paramref name="exception"/> is <c>null</c>.
        /// </exception>
        public static bool TrySetExceptionOrCanceled(
            this ValueTaskCompletionSource taskCompletionSource,
            Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (exception is OperationCanceledException operationCanceledException)
            {
                var cancellationToken = operationCanceledException.CancellationToken;

                if (cancellationToken != default)
                {
                    return taskCompletionSource.TrySetCanceled(cancellationToken);
                }

                return taskCompletionSource.TrySetCanceled();
            }

            return taskCompletionSource.TrySetException(exception);
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask"/> object
        /// into the <see cref="TaskStatus.Faulted"/> or <see cref="TaskStatus.Canceled"/> state,
        /// depending on the type of exception.
        /// </summary>
        /// <param name="taskCompletionSource">The task completion source.</param>
        /// <param name="exception">The exception to bind to the <see cref="ValueTask"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="taskCompletionSource"/> or <paramref name="exception"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The <see cref="ValueTask"/> is already in one of the three final states:
        /// <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/>,
        /// or <see cref="TaskStatus.Canceled"/>.
        /// </exception>
        public static void SetExceptionOrCanceled(
            this ValueTaskCompletionSource taskCompletionSource,
            Exception exception)
        {
            if (!TrySetExceptionOrCanceled(taskCompletionSource, exception))
            {
                throw new InvalidOperationException(
                    "The underlying Task<TResult> is already in one of the three final states:" +
                    " RanToCompletion, Faulted, or Canceled.");
            }
        }
    }
}
