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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils
{
    [TestClass]
    public class EventInvocationHelperTests
    {
        [TestMethod]
        public void InvokeTest()
        {
            var actionInvoked = new bool[3];

            Action @delegate = null;

            void Action1()
            {
                actionInvoked[0] = true;
            }

            void Action2()
            {
                actionInvoked[1] = true;
            }

            void Action3()
            {
                actionInvoked[2] = true;
            }

            @delegate += Action1;
            @delegate += Action2;
            @delegate += Action3;

            AI4EUtilsDelegateExtensions.InvokeAll(@delegate, d => d());

            Assert.IsTrue(actionInvoked.All());
        }

        [TestMethod]
        public void InvokeExceptionTest()
        {
            var actionInvoked = new bool[3];

            Action @delegate = null;

            void Action1()
            {
                actionInvoked[0] = true;
            }

            void Action2()
            {
                actionInvoked[1] = true;
                throw new CustomException();
            }

            void Action3()
            {
                actionInvoked[2] = true;
            }

            @delegate += Action1;
            @delegate += Action2;
            @delegate += Action3;

            Assert.ThrowsException<CustomException>(() =>
            {
                AI4EUtilsDelegateExtensions.InvokeAll(@delegate, d => d());
            });

            Assert.IsTrue(actionInvoked.All());
        }

        [TestMethod]
        public void InvokeMultipleExceptionTest()
        {
            var actionInvoked = new bool[3];

            Action @delegate = null;

            void Action1()
            {
                actionInvoked[0] = true;
            }

            void Action2()
            {
                actionInvoked[1] = true;
                throw new CustomException();
            }

            void Action3()
            {
                actionInvoked[2] = true;
                throw new CustomException2();
            }

            @delegate += Action1;
            @delegate += Action2;
            @delegate += Action3;

            var exception = Assert.ThrowsException<AggregateException>(() =>
            {
                AI4EUtilsDelegateExtensions.InvokeAll(@delegate, d => d());
            });

            Assert.IsTrue(actionInvoked.All());
            Assert.AreEqual(2, exception.InnerExceptions.Count());
            Assert.IsTrue(exception.InnerExceptions.Any(p => p is CustomException));
            Assert.IsTrue(exception.InnerExceptions.Any(p => p is CustomException2));
        }

        [TestMethod]
        public async Task InvokeAsyncTest()
        {
            var actionInvoked = new bool[3];

            Func<ValueTask> @delegate = null;

            ValueTask Action1()
            {
                actionInvoked[0] = true;
                return default;
            }

            ValueTask Action2()
            {
                actionInvoked[1] = true;
                return default;
            }

            ValueTask Action3()
            {
                actionInvoked[2] = true;
                return default;
            }

            @delegate += Action1;
            @delegate += Action2;
            @delegate += Action3;

            await AI4EUtilsDelegateExtensions.InvokeAllAsync(@delegate, d => d());

            Assert.IsTrue(actionInvoked.All());
        }

        [TestMethod]
        public async Task InvokeAsyncExceptionTest()
        {
            var actionInvoked = new bool[3];

            Func<ValueTask> @delegate = null;

            ValueTask Action1()
            {
                actionInvoked[0] = true;
                return default;
            }

            ValueTask Action2()
            {
                actionInvoked[1] = true;
                throw new CustomException();
            }

            ValueTask Action3()
            {
                actionInvoked[2] = true;
                return default;
            }

            @delegate += Action1;
            @delegate += Action2;
            @delegate += Action3;

            await Assert.ThrowsExceptionAsync<CustomException>(async () =>
            {
                await AI4EUtilsDelegateExtensions.InvokeAllAsync(@delegate, d => d());
            });

            Assert.IsTrue(actionInvoked.All());
        }

        [TestMethod]
        public async Task InvokeAsyncMultipleExceptionTest()
        {
            var actionInvoked = new bool[3];

            Func<ValueTask> @delegate = null;

            ValueTask Action1()
            {
                actionInvoked[0] = true;
                return default;
            }

            ValueTask Action2()
            {
                actionInvoked[1] = true;
                throw new CustomException();
            }

#pragma warning disable CS1998
            async ValueTask Action3()
#pragma warning restore CS1998
            {
                actionInvoked[2] = true;
                throw new CustomException2();
            }

            @delegate += Action1;
            @delegate += Action2;
            @delegate += Action3;

            var exception = await Assert.ThrowsExceptionAsync<AggregateException>(async () =>
            {
                await AI4EUtilsDelegateExtensions.InvokeAllAsync(@delegate, d => d());
            });

            Assert.IsTrue(actionInvoked.All());
            Assert.AreEqual(2, exception.InnerExceptions.Count());
            Assert.IsTrue(exception.InnerExceptions.Any(p => p is CustomException));
            Assert.IsTrue(exception.InnerExceptions.Any(p => p is CustomException2));
        }

        private sealed class CustomException : Exception { }
        private sealed class CustomException2 : Exception { }
    }
}
