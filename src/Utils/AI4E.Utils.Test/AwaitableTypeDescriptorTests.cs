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

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils
{
    [TestClass]
    public class AwaitableTypeDescriptorTests
    {
        [TestMethod]
        public void TaskDescriptionTest()
        {
            var taskType = typeof(Task);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            Assert.IsTrue(descriptor.IsAwaitable);
            Assert.AreEqual(taskType, descriptor.Type);
            Assert.AreEqual(typeof(void), descriptor.ResultType);
            Assert.AreEqual(typeof(TaskAwaiter), descriptor.AwaiterType);
        }

        [TestMethod]
        public void TaskOfTDescriptionTest()
        {
            var taskType = typeof(Task<string>);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            Assert.IsTrue(descriptor.IsAwaitable);
            Assert.AreEqual(taskType, descriptor.Type);
            Assert.AreEqual(typeof(string), descriptor.ResultType);
            Assert.AreEqual(typeof(TaskAwaiter<string>), descriptor.AwaiterType);
        }

        [TestMethod]
        public void ValueTaskDescriptionTest()
        {
            var taskType = typeof(ValueTask);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            Assert.IsTrue(descriptor.IsAwaitable);
            Assert.AreEqual(taskType, descriptor.Type);
            Assert.AreEqual(typeof(void), descriptor.ResultType);
            Assert.AreEqual(typeof(ValueTaskAwaiter), descriptor.AwaiterType);
        }

        [TestMethod]
        public void ValueTaskOfTDescriptionTest()
        {
            var taskType = typeof(ValueTask<string>);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            Assert.IsTrue(descriptor.IsAwaitable);
            Assert.AreEqual(taskType, descriptor.Type);
            Assert.AreEqual(typeof(string), descriptor.ResultType);
            Assert.AreEqual(typeof(ValueTaskAwaiter<string>), descriptor.AwaiterType);
        }

        [TestMethod]
        public void NonAwaitableDescriptionTest()
        {
            var type = typeof(string);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(type);

            Assert.IsFalse(descriptor.IsAwaitable);
            Assert.AreEqual(type, descriptor.Type);
            Assert.AreEqual(typeof(string), descriptor.ResultType);
            Assert.IsNull(descriptor.AwaiterType);
        }

        [TestMethod]
        public async Task TaskAwaitTest()
        {
            var taskType = typeof(Task);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            var awaitable = descriptor.GetAwaitable(DelayTask());
            await awaitable;
        }

        [TestMethod]
        public async Task TaskOfTAwaitTest()
        {
            var taskType = typeof(Task<int>);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            var awaitable = descriptor.GetAwaitable(DelayTaskOfT());
            var result = await awaitable;

            Assert.AreEqual(14, result);
        }

        [TestMethod]
        public async Task ValueTaskAwaitTest()
        {
            var taskType = typeof(ValueTask);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            var awaitable = descriptor.GetAwaitable(DelayValueTask());
            await awaitable;
        }

        [TestMethod]
        public async Task ValueTaskOfTAwaitTest()
        {
            var taskType = typeof(ValueTask<int>);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(taskType);

            var awaitable = descriptor.GetAwaitable(DelayValueTaskOfT());
            var result = await awaitable;

            Assert.AreEqual(14, result);
        }

        [TestMethod]
        public async Task AwaitDefaultAsyncTypeAwaitableTest()
        {
            await default(AsyncTypeAwaitable);
        }

        [TestMethod]
        public async Task AwaitNonAwaitableTypeTest()
        {
            var type = typeof(string);
            var descriptor = AwaitableTypeDescriptor.GetTypeDescriptor(type);
            var str = "jknorvn";

            var result = await descriptor.GetAwaitable(str);

            Assert.AreSame(str, result);
        }

        private async Task DelayTask()
        {
            await Task.Delay(10);
        }

        private async Task<int> DelayTaskOfT()
        {
            await Task.Delay(19);
            return 14;
        }

        private async ValueTask DelayValueTask()
        {
            await Task.Delay(10);
        }

        private async ValueTask<int> DelayValueTaskOfT()
        {
            await Task.Delay(19);
            return 14;
        }
    }
}
