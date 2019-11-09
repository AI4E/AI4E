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
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Async
{
    [TestClass]
    public class Issue22
    {
        [TestMethod]
        public async Task DisposalThrowsTest()
        {
            var lazy = new DisposableAsyncLazy<byte>(
                _ => { return Task.FromResult<byte>(12); },
                p => { throw new CustomException(); },
                DisposableAsyncLazyOptions.None);

            await lazy;

            await Assert.ThrowsExceptionAsync<CustomException>(async () =>
            {
                await lazy.DisposeAsync();
            });

            Assert.AreEqual(TaskStatus.RanToCompletion, lazy.GetDisposeTask().Status);
        }

        [Serializable]
        private sealed class CustomException : Exception { }
    }

    public static class DisposableAsyncLazyTestExtensions
    {
        public static Task GetDisposeTask<T>(this DisposableAsyncLazy<T> instance)
        {
            var field = typeof(DisposableAsyncLazy<T>).GetField("_disposeTask", BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(instance) as Task;
        }
    }
}
