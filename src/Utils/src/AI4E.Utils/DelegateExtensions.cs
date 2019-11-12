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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace System
{
    /// <summary>
    /// Contains helper for invoking all members of a delegate separately.
    /// </summary>
    public static class AI4EUtilsDelegateExtensions
    {
        public static TDelegate? Combine<TDelegate>(this IEnumerable<TDelegate?> delegates)
            where TDelegate : notnull, Delegate
        {
            if (delegates is Delegate?[] delegateArray)
            {
                return (TDelegate?)Delegate.Combine(delegateArray);
            }

            var count = delegates.Count(p => !(p is null));

            if (count == 0)
            {
                return null;
            }

            if (count == 1)
            {
                return delegates.First();
            }

            if (count == 2)
            {
                return (TDelegate?)Delegate.Combine(delegates.First(), delegates.Last());
            }

            delegateArray = new Delegate?[count];

            var i = 0;
#pragma warning disable CA1062
            foreach (var d in delegates)
#pragma warning restore CA1062
            {
                if (d is null)
                    continue;

                delegateArray[i++] = d;
            }

            return (TDelegate?)Delegate.Combine(delegateArray);
        }


        /// <summary>
        /// Invokes all members of a delegate's invokation list.
        /// </summary>
        /// <typeparam name="TDelegate">The type of delegate.</typeparam>
        /// <param name="delegate">The delegate that defines the invokation list.</param>
        /// <param name="invocation">Defines the invocation of the delegate.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="invocation"/> is null.</exception>
        /// <remarks>
        /// Other than invoking the delegate directly, this ensures that each invokation list member is invoked
        /// regardless of other members throwing exceptions. If one invokation list member throws an exception,
        /// this exception is rethrown, if multiple list members throw exceptions, an aggregate exception is thrown.
        /// </remarks>
        public static void InvokeAll<TDelegate>(this TDelegate @delegate, Action<TDelegate> invocation)
            where TDelegate : Delegate
        {
            if (invocation == null)
                throw new ArgumentNullException(nameof(invocation));

#pragma warning disable CA1062
            var invocationList = @delegate.GetInvocationList();
#pragma warning restore CA1062
            List<Exception>? capturedExceptions = null;

            foreach (var singlecastDelegate in invocationList)
            {
                try
                {
                    invocation((TDelegate)singlecastDelegate);
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    if (capturedExceptions == null)
                    {
                        capturedExceptions = new List<Exception>();
                    }

                    capturedExceptions.Add(exc);
                }
            }

            if (capturedExceptions == null || !capturedExceptions.Any())
            {
                return;
            }

            if (capturedExceptions.Count == 1)
            {
                throw capturedExceptions.First();
            }
            else
            {
                throw new AggregateException(capturedExceptions);
            }
        }

        /// <summary>
        /// Invokes all members of a delegate's invokation list.
        /// </summary>
        /// <param name="action">The delegate that defines the invokation list.</param>
        /// <remarks>
        /// Other than invoking the delegate directly, this ensures that each invokation list member is invoked
        /// regardless of other members throwing exceptions. If one invokation list member throws an exception,
        /// this exception is rethrown, if multiple list members throw exceptions, an aggregate exception is thrown.
        /// </remarks>
        public static void InvokeAll(this Action action)
        {
            InvokeAll(action, a => a());
        }

        /// <summary>
        /// Invokes all members of a delegate's invokation list.
        /// </summary>
        /// <param name="eventHandler">The delegate that defines the invokation list.</param>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains no event data.</param>
        /// <remarks>
        /// Other than invoking the delegate directly, this ensures that each invokation list member is invoked
        /// regardless of other members throwing exceptions. If one invokation list member throws an exception,
        /// this exception is rethrown, if multiple list members throw exceptions, an aggregate exception is thrown.
        /// </remarks>
        public static void InvokeAll(this EventHandler eventHandler, object sender, EventArgs e)
        {
            InvokeAll(eventHandler, h => h(sender, e));
        }

        /// <summary>
        /// Invokes all members of a delegate's invokation list.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event data generated by the event.</typeparam>
        /// <param name="eventHandler">The delegate that defines the invokation list.</param>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the event data.</param>
        /// <remarks>
        /// Other than invoking the delegate directly, this ensures that each invokation list member is invoked
        /// regardless of other members throwing exceptions. If one invokation list member throws an exception,
        /// this exception is rethrown, if multiple list members throw exceptions, an aggregate exception is thrown.
        /// </remarks>
        public static void InvokeAll<TEventArgs>(this EventHandler<TEventArgs> eventHandler, object sender, TEventArgs e)
        {
            InvokeAll(eventHandler, h => h(sender, e));
        }

        /// <summary>
        /// Asynchronously invokes all members of a delegate's invokation list.
        /// </summary>
        /// <typeparam name="TDelegate">The type of delegate.</typeparam>
        /// <param name="delegate">The delegate that defines the invokation list.</param>
        /// <param name="invocation">Defines the invocation of the delegate.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="invocation"/> is null.</exception>
        /// <remarks>
        /// Other than invoking the delegate directly, this ensures that each invokation list member is invoked
        /// regardless of other members throwing exceptions. If one invokation list member throws an exception,
        /// this exception is rethrown, if multiple list members throw exceptions, an aggregate exception is thrown.
        /// </remarks>
        public static async ValueTask InvokeAllAsync<TDelegate>(this TDelegate @delegate, Func<TDelegate, ValueTask> invocation)
            where TDelegate : Delegate
        {
            if (invocation == null)
                throw new ArgumentNullException(nameof(invocation));

#pragma warning disable CA1062
            var invocationList = @delegate.GetInvocationList();
#pragma warning restore CA1062
            List<Exception>? capturedExceptions = null;

            List<Exception> GetCapturedExceptions()
            {
                var result = Volatile.Read(ref capturedExceptions);

                if (result == null)
                {
                    result = new List<Exception>();
                    var current = Interlocked.CompareExchange(ref capturedExceptions, result, null);
                    if (current != null)
                    {
                        result = current;
                    }
                }

                return result;
            }

            async ValueTask InvokeCoreAsync(TDelegate singlecastDelegate)
            {
                try
                {
                    await invocation(singlecastDelegate);
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    var exceptions = GetCapturedExceptions();

                    lock (exceptions)
                    {
                        exceptions.Add(exc);
                    }
                }
            }

            await invocationList.Select(p => InvokeCoreAsync((TDelegate)p)).WhenAll();

            if (capturedExceptions == null || !capturedExceptions.Any())
            {
                return;
            }

            if (capturedExceptions.Count == 1)
            {
                throw capturedExceptions.First();
            }
            else
            {
                throw new AggregateException(capturedExceptions);
            }
        }

        /// <summary>
        /// Asynchronously invokes all members of a delegate's invokation list.
        /// </summary>
        /// <param name="action">The delegate that defines the invokation list.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Other than invoking the delegate directly, this ensures that each invokation list member is invoked
        /// regardless of other members throwing exceptions. If one invokation list member throws an exception,
        /// this exception is rethrown, if multiple list members throw exceptions, an aggregate exception is thrown.
        /// </remarks>
        public static ValueTask InvokeAllAsync(this Func<ValueTask> action)
        {
            return InvokeAllAsync(action, a => a());
        }

        /// <summary>
        /// Asynchronously invokes all members of a delegate's invokation list.
        /// </summary>
        /// <param name="action">The delegate that defines the invokation list.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Other than invoking the delegate directly, this ensures that each invokation list member is invoked
        /// regardless of other members throwing exceptions. If one invokation list member throws an exception,
        /// this exception is rethrown, if multiple list members throw exceptions, an aggregate exception is thrown.
        /// </remarks>
        public static ValueTask InvokeAllAsync(this Func<Task> action)
        {
            return InvokeAllAsync(action, a => a().AsValueTask());
        }
    }
}
