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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;

namespace AI4E.Utils.Async
{
    [TestClass]
    public class DisposableAsyncLazyTests
    {
        [TestMethod]
        public void IsNotAutostartedTest()
        {
            var lazy = new DisposableAsyncLazy<byte>(
                _ => { throw null; },
                _ => { throw null; },
                DisposableAsyncLazyOptions.None);

            Assert.IsFalse(lazy.IsStarted);
        }

        [TestMethod]
        public async Task FactoryTest()
        {
            var lazy = new DisposableAsyncLazy<byte>(
                cancellation => { return Task.FromResult<byte>(12); },
                _ => { throw null; },
                DisposableAsyncLazyOptions.None);

            Assert.AreEqual(12, await lazy);
        }

        //[TestMethod]
        public async Task ExecuteOnCallingThreadTest()
        {
            using (var context = new AsyncContext())
            {
                SynchronizationContext.SetSynchronizationContext(context.SynchronizationContext);

                var threadId = Thread.CurrentThread.ManagedThreadId;

                var lazy = new DisposableAsyncLazy<byte>(
                    cancellation =>
                    {
                        Assert.AreEqual(threadId, Thread.CurrentThread.ManagedThreadId);

                        return Task.FromResult<byte>(12);
                    },
                    _ => { throw null; },
                    DisposableAsyncLazyOptions.None);

                await lazy;
            }
        }

        [TestMethod]
        public async Task AutostartTest()
        {
            var lazy = new DisposableAsyncLazy<byte>(
                cancellation => { return Task.FromResult<byte>(12); },
                _ => { throw null; },
                DisposableAsyncLazyOptions.Autostart);

            Assert.IsTrue(lazy.IsStarted);
            Assert.AreEqual(12, await lazy);
        }

        [TestMethod]
        public async Task RetryOnFailureTest()
        {
            var @try = 1;

            var lazy = new DisposableAsyncLazy<byte>(
                cancellation =>
                {
                    if (@try == 1)
                    {
                        @try++;
                        throw new Exception();
                    }

                    return Task.FromResult<byte>(12);
                },
                _ => { throw null; },
                DisposableAsyncLazyOptions.RetryOnFailure);

            Assert.IsFalse(lazy.IsStarted);
            await Assert.ThrowsExceptionAsync<Exception>(async () => await lazy);
            Assert.AreEqual(12, await lazy);
        }

        [TestMethod]
        public async Task DisposeTest()
        {
            var disposeCalled = false;

            var lazy = new DisposableAsyncLazy<byte>(
                _ => { return Task.FromResult<byte>(12); },
                p => { Assert.AreEqual(12, p); disposeCalled = true; return Task.CompletedTask; },
                DisposableAsyncLazyOptions.None);

            await lazy;

            await lazy.DisposeAsync();
            Assert.IsTrue(disposeCalled);
        }

        [TestMethod]
        public async Task DisposeIfNotYetStartedTest()
        {
            var disposeCalled = false;

            var lazy = new DisposableAsyncLazy<byte>(
                _ => { return Task.FromResult<byte>(12); },
                p => { Assert.AreEqual(12, p); disposeCalled = true; return Task.CompletedTask; },
                DisposableAsyncLazyOptions.None);

            await lazy.DisposeAsync();
            Assert.IsFalse(disposeCalled);
        }

        [TestMethod]
        public async Task DisposeIfDisposedTest()
        {
            var disposeCalled = false;

            var lazy = new DisposableAsyncLazy<byte>(
                _ => { return Task.FromResult<byte>(12); },
                p => { Assert.AreEqual(12, p); disposeCalled = true; return Task.CompletedTask; },
                DisposableAsyncLazyOptions.None);

            await lazy;

            await lazy.DisposeAsync();
            disposeCalled = false;
            await lazy.DisposeAsync();
            Assert.IsFalse(disposeCalled);
        }

        [TestMethod]
        public async Task DisposalThrowsTest()
        {
            var lazy = new DisposableAsyncLazy<byte>(
                _ => { return Task.FromResult<byte>(12); },
                p => { throw new Exception(); },
                DisposableAsyncLazyOptions.None);

            await lazy;

            await Assert.ThrowsExceptionAsync<Exception>(async () =>
            {
                await lazy.DisposeAsync();
            });
        }
    }
}
