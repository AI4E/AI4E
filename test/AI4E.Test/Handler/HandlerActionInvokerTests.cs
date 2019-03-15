/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2019 Andreas Truetschel and contributors.
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

namespace AI4E.Handler
{
    [TestClass]
    public class HandlerActionInvokerTests
    {
        [TestMethod]
        public async Task SyncTargetVoidResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.SyncTargetVoidResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();
            var message = "MyMessage";
            Func<object> parameter = () => null;

            object ParameterResolver(ParameterInfo p)
            {
                Assert.AreEqual("parameter", p.Name);
                Assert.AreEqual(typeof(Func<object>), p.ParameterType);

                return parameter;
            }

            var resultTask = invoker.InvokeAsync(instance, message, ParameterResolver);

            Assert.AreEqual(typeof(string), invoker.FirstParameterType);
            Assert.AreEqual(typeof(void), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.IsNull(await resultTask);
            Assert.AreEqual(message, instance.Message);
            Assert.AreEqual(parameter, instance.Parameter);
        }

        [TestMethod]
        public async Task SyncTargetIntResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.SyncTargetIntResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();
            var message = "MyMessage";
            Func<object> parameter = () => null;

            object ParameterResolver(ParameterInfo p)
            {
                Assert.AreEqual("parameter", p.Name);
                Assert.AreEqual(typeof(Func<object>), p.ParameterType);

                return parameter;
            }

            var resultTask = invoker.InvokeAsync(instance, message, ParameterResolver);

            Assert.AreEqual(typeof(string), invoker.FirstParameterType);
            Assert.AreEqual(typeof(int), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.AreEqual(42, await resultTask);
            Assert.AreEqual(message, instance.Message);
            Assert.AreEqual(parameter, instance.Parameter);
        }

        [TestMethod]
        public async Task AsyncTargetVoidResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.AsyncTargetVoidResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();
            var message = "MyMessage";
            Func<object> parameter = () => null;

            object ParameterResolver(ParameterInfo p)
            {
                Assert.AreEqual("parameter", p.Name);
                Assert.AreEqual(typeof(Func<object>), p.ParameterType);

                return parameter;
            }

            var resultTask = invoker.InvokeAsync(instance, message, ParameterResolver);

            Assert.AreEqual(typeof(string), invoker.FirstParameterType);
            Assert.AreEqual(typeof(void), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.IsNull(await resultTask);
            Assert.AreEqual(message, instance.Message);
            Assert.AreEqual(parameter, instance.Parameter);
        }

        [TestMethod]
        public async Task AsyncTargetIntResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.AsyncTargetIntResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();
            var message = "MyMessage";
            Func<object> parameter = () => null;

            object ParameterResolver(ParameterInfo p)
            {
                Assert.AreEqual("parameter", p.Name);
                Assert.AreEqual(typeof(Func<object>), p.ParameterType);

                return parameter;
            }

            var resultTask = invoker.InvokeAsync(instance, message, ParameterResolver);

            Assert.AreEqual(typeof(string), invoker.FirstParameterType);
            Assert.AreEqual(typeof(int), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.AreEqual(42, await resultTask);
            Assert.AreEqual(message, instance.Message);
            Assert.AreEqual(parameter, instance.Parameter);
        }

        [TestMethod]
        public async Task NoParametersSyncTargetVoidResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.NoParametersSyncTargetVoidResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();

            object ParameterResolver(ParameterInfo p)
            {
                Assert.Fail();
                return null;
            }

            var resultTask = invoker.InvokeAsync(instance, null, ParameterResolver);

            Assert.AreEqual(typeof(void), invoker.FirstParameterType);
            Assert.AreEqual(typeof(void), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.IsNull(await resultTask);
        }

        [TestMethod]
        public async Task NoParametersSyncTargetIntResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.NoParametersSyncTargetIntResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();

            object ParameterResolver(ParameterInfo p)
            {
                Assert.Fail();
                return null;
            }

            var resultTask = invoker.InvokeAsync(instance, null, ParameterResolver);

            Assert.AreEqual(typeof(void), invoker.FirstParameterType);
            Assert.AreEqual(typeof(int), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.AreEqual(42, await resultTask);
        }

        [TestMethod]
        public async Task NoParametersAsyncTargetVoidResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.NoParametersAsyncTargetVoidResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();

            object ParameterResolver(ParameterInfo p)
            {
                Assert.Fail();
                return null;
            }

            var resultTask = invoker.InvokeAsync(instance, null, ParameterResolver);

            Assert.AreEqual(typeof(void), invoker.FirstParameterType);
            Assert.AreEqual(typeof(void), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.IsNull(await resultTask);
        }

        [TestMethod]
        public async Task NoParametersAsyncTargetIntResultTest()
        {
            var target = typeof(HandlerActionInvokerTargets).GetMethod(nameof(HandlerActionInvokerTargets.NoParametersAsyncTargetIntResult));
            var invoker = HandlerActionInvoker.GetInvoker(target);
            var instance = new HandlerActionInvokerTargets();

            object ParameterResolver(ParameterInfo p)
            {
                Assert.Fail();
                return null;
            }

            var resultTask = invoker.InvokeAsync(instance, null, ParameterResolver);

            Assert.AreEqual(typeof(void), invoker.FirstParameterType);
            Assert.AreEqual(typeof(int), invoker.ReturnTypeDescriptor.ResultType);

            Assert.IsTrue(resultTask.IsCompletedSuccessfully);
            Assert.AreEqual(42, await resultTask);
        }
    }

    public class HandlerActionInvokerTargets
    {
        public string Message { get; set; }
        public Func<object> Parameter { get; set; }

        public void SyncTargetVoidResult(string message, Func<object> parameter)
        {
            Message = message;
            Parameter = parameter;
        }

        public int SyncTargetIntResult(string message, Func<object> parameter)
        {
            Message = message;
            Parameter = parameter;

            return 42;
        }

        public Task AsyncTargetVoidResult(string message, Func<object> parameter)
        {
            Message = message;
            Parameter = parameter;
            return Task.CompletedTask;
        }

        public Task<int> AsyncTargetIntResult(string message, Func<object> parameter)
        {
            Message = message;
            Parameter = parameter;

            return Task.FromResult(42);
        }

        public void NoParametersSyncTargetVoidResult() { }

        public int NoParametersSyncTargetIntResult()
        {
            return 42;
        }

        public Task NoParametersAsyncTargetVoidResult()
        { 
            return Task.CompletedTask;
        }

        public Task<int> NoParametersAsyncTargetIntResult()
        {
            return Task.FromResult(42);
        }
    }
}
