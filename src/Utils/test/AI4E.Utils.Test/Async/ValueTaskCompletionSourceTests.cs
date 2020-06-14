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
using AI4E.Utils.TestTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Async
{
    [TestClass]
    public class ValueTaskCompletionSourceTests
    {
        [TestMethod]
        public void DefaultYieldsDefaultTaskTest()
        {
            var valueTaskCompletionSource = GetDefault();
            var valueTask = valueTaskCompletionSource.Task;

            Assert.IsTrue(valueTask.IsCompletedSuccessfully);
        }

        [TestMethod]
        public void DefaultTrySetResultTest()
        {
            var valueTaskCompletionSource = GetDefault();

            Assert.IsFalse(valueTaskCompletionSource.TrySetResult());
        }

        [TestMethod]
        public void DefaultTrySetExceptionTest()
        {
            var valueTaskCompletionSource = GetDefault();

            Assert.IsFalse(valueTaskCompletionSource.TrySetException(new Exception()));
            Assert.IsFalse(valueTaskCompletionSource.TrySetException(new Exception().Yield()));
        }

        [TestMethod]
        public void DefaultTrySetCanceledTest()
        {
            var valueTaskCompletionSource = GetDefault();

            Assert.IsFalse(valueTaskCompletionSource.TrySetCanceled());
            Assert.IsFalse(valueTaskCompletionSource.TrySetCanceled(default));
        }

        [TestMethod]
        public void DefaultSetResultTest()
        {
            var valueTaskCompletionSource = GetDefault();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetResult();
            });
        }

        [TestMethod]
        public void DefaultSetExceptionTest()
        {
            var valueTaskCompletionSource = GetDefault();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetException(new Exception());
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetException(new Exception().Yield());
            });
        }

        [TestMethod]
        public void DefaultSetCanceledTest()
        {
            var valueTaskCompletionSource = GetDefault();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetCanceled();
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetCanceled(default);
            });
        }

        private ValueTaskCompletionSource GetDefault()
        {
            return default;
        }

        [TestMethod]
        public void CreateTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            Assert.IsFalse(valueTask.IsCompleted);
        }

        [TestMethod]
        public void CreateOnCompletedTest()
        {
            var continuationInvoked = false;

            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().OnCompleted(() =>
            {
                continuationInvoked = true;
            });

            Assert.IsFalse(continuationInvoked);
        }

        [TestMethod]
        public void CreateUnsafeOnCompletedTest()
        {
            var continuationInvoked = false;

            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().UnsafeOnCompleted(() =>
            {
                continuationInvoked = true;
            });

            Assert.IsFalse(continuationInvoked);
        }

        [TestMethod]
        public void SetResultTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTaskCompletionSource.SetResult();

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsCompletedSuccessfully);
        }

        [TestMethod]
        public void TrySetResultTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsCompletedSuccessfully);
        }

        [TestMethod]
        public void SetResultOnCompletedTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().OnCompleted(() =>
            {
                continuationInvoked = true;
            });

            valueTaskCompletionSource.SetResult();

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void TrySetResultOnCompletedTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().OnCompleted(() =>
            {
                continuationInvoked = true;
            });

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void SetResultUnsafeOnCompletedTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().UnsafeOnCompleted(() =>
            {
                continuationInvoked = true;
            });

            valueTaskCompletionSource.SetResult();

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void TrySetResultUnsafeOnCompletedTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().UnsafeOnCompleted(() =>
            {
                continuationInvoked = true;
            });

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void SetResultOnCompletedContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                var context = SynchronizationContext.Current;

                valueTask.GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(context, SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            valueTaskCompletionSource.SetResult();

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void TrySetResultOnCompletedContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                var context = SynchronizationContext.Current;

                valueTask.GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(context, SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void SetResultUnsafeOnCompletedContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                var context = SynchronizationContext.Current;

                valueTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(context, SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            valueTaskCompletionSource.SetResult();

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void TrySetResultUnsafeOnCompletedContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                var context = SynchronizationContext.Current;

                valueTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(context, SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void SetResultOnCompletedNoContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                valueTask.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    Assert.IsNull(SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            valueTaskCompletionSource.SetResult();

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void TrySetResultOnCompletedNoContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                valueTask.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    Assert.IsNull(SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void SetResultUnsafeOnCompletedNoContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                valueTask.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.IsNull(SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            valueTaskCompletionSource.SetResult();

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void TrySetResultUnsafeOnCompletedNoContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                valueTask.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.IsNull(SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task SetResultOnCompletedSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, taskScheduler);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            valueTaskCompletionSource.SetResult();
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task TrySetResultOnCompletedSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, taskScheduler);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task SetResultUnsafeOnCompletedSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, taskScheduler);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            valueTaskCompletionSource.SetResult();
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task TrySetResultUnsafeOnCompletedSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, taskScheduler);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task SetResultOnCompletedNoSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, TaskScheduler.Default);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            valueTaskCompletionSource.SetResult();
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task TrySetResultOnCompletedNoSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, TaskScheduler.Default);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task SetResultUnsafeOnCompletedNoSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, TaskScheduler.Default);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            valueTaskCompletionSource.SetResult();
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task TrySetResultUnsafeOnCompletedNoSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, TaskScheduler.Default);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            Assert.IsTrue(valueTaskCompletionSource.TrySetResult());
            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task SetExceptionTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var exception = new CustomException();

            valueTaskCompletionSource.SetException(exception);

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsFaulted);
            Assert.IsFalse(valueTask.IsCompletedSuccessfully);
            await Assert.ThrowsExceptionAsync<CustomException>(async () =>
            {
                await valueTask;
            });
        }

        [TestMethod]
        public async Task TrySetExceptionTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var exception = new CustomException();

            Assert.IsTrue(valueTaskCompletionSource.TrySetException(exception));

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsFaulted);
            Assert.IsFalse(valueTask.IsCompletedSuccessfully);
            await Assert.ThrowsExceptionAsync<CustomException>(async () =>
            {
                await valueTask;
            });
        }

        [TestMethod]
        public async Task SetException2Test()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var exceptions = new Exception[] { new CustomException(), new ArgumentNullException() };

            valueTaskCompletionSource.SetException(exceptions);

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsFaulted);
            Assert.IsFalse(valueTask.IsCompletedSuccessfully);
            await Assert.ThrowsExceptionAsync<CustomException>(async () =>
            {
                await valueTask;
            });
        }

        [TestMethod]
        public async Task TrySetException2Test()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var exceptions = new Exception[] { new CustomException(), new ArgumentNullException() };

            Assert.IsTrue(valueTaskCompletionSource.TrySetException(exceptions));

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsFaulted);
            Assert.IsFalse(valueTask.IsCompletedSuccessfully);
            await Assert.ThrowsExceptionAsync<CustomException>(async () =>
            {
                await valueTask;
            });
        }

        [TestMethod]
        public async Task SetCanceledTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            valueTaskCompletionSource.SetCanceled();

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsCanceled);
            Assert.IsFalse(valueTask.IsCompletedSuccessfully);
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await valueTask;
            });
        }

        [TestMethod]
        public async Task TrySetCanceledTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            var exception = new CustomException();

            Assert.IsTrue(valueTaskCompletionSource.TrySetCanceled());

            Assert.IsTrue(valueTask.IsCompleted);
            Assert.IsTrue(valueTask.IsCanceled);
            Assert.IsFalse(valueTask.IsCompletedSuccessfully);
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await valueTask;
            });
        }

        private sealed class CustomException : Exception { }

        [TestMethod]
        public void CompletedTrySetResultTest()
        {
            var valueTaskCompletionSource = GetCompleted();

            Assert.IsFalse(valueTaskCompletionSource.TrySetResult());
        }

        [TestMethod]
        public void CompletedTrySetExceptionTest()
        {
            var valueTaskCompletionSource = GetCompleted();

            Assert.IsFalse(valueTaskCompletionSource.TrySetException(new Exception()));
            Assert.IsFalse(valueTaskCompletionSource.TrySetException(new Exception().Yield()));
        }

        [TestMethod]
        public void CompletedTrySetCanceledTest()
        {
            var valueTaskCompletionSource = GetCompleted();

            Assert.IsFalse(valueTaskCompletionSource.TrySetCanceled());
            Assert.IsFalse(valueTaskCompletionSource.TrySetCanceled(default));
        }

        [TestMethod]
        public void CompletedSetResultTest()
        {
            var valueTaskCompletionSource = GetCompleted();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetResult();
            });
        }

        [TestMethod]
        public void CompletedSetExceptionTest()
        {
            var valueTaskCompletionSource = GetCompleted();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetException(new Exception());
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetException(new Exception().Yield());
            });
        }

        [TestMethod]
        public void CompletedSetCanceledTest()
        {
            var valueTaskCompletionSource = GetCompleted();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetCanceled();
            });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTaskCompletionSource.SetCanceled(default);
            });
        }

        [TestMethod]
        public async Task CompletedOnCompletedTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().OnCompleted(() =>
            {
                continuationInvoked = true;
            });

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task CompletedUnsafeOnCompletedTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().UnsafeOnCompleted(() =>
            {
                continuationInvoked = true;
            });

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void CompletedOnCompletedContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                var context = SynchronizationContext.Current;

                valueTask.GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(context, SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public void CompletedUnsafeOnCompletedContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                var context = SynchronizationContext.Current;

                valueTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(context, SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task CompletedOnCompletedNoContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                valueTask.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    Assert.IsNull(SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task CompletedUnsafeOnCompletedNoContextTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;

            using (TestSynchronizationContext.Use())
            {
                valueTask.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.IsNull(SynchronizationContext.Current);
                    continuationInvoked = true;
                });
            }

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task CompletedOnCompletedSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, taskScheduler);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task CompletedUnsafeOnCompletedSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, taskScheduler);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task CompletedOnCompletedNoSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, TaskScheduler.Default);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        [TestMethod]
        public async Task CompletedUnsafeOnCompletedNoSchedulerTest()
        {
            var continuationInvoked = false;
            var valueTaskCompletionSource = GetCompleted();
            var valueTask = valueTaskCompletionSource.Task;
            var taskScheduler = new TestTaskScheduler();

            await Task.Factory.StartNew(() =>
            {
                valueTask.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(() =>
                {
                    Assert.AreSame(TaskScheduler.Current, TaskScheduler.Default);
                    continuationInvoked = true;
                });
            }, default, TaskCreationOptions.DenyChildAttach, taskScheduler);

            await Task.Delay(20);
            Assert.IsTrue(continuationInvoked);
        }

        private ValueTaskCompletionSource GetCompleted()
        {
            var result = ValueTaskCompletionSource.Create();
            result.SetResult();
            return result;
        }

        [TestMethod]
        public void MultipleContinuationsThrowTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            valueTask.GetAwaiter().OnCompleted(() => { });

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTask.GetAwaiter().OnCompleted(() => { });
            });
        }

        [TestMethod]
        public void GetResultTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            valueTaskCompletionSource.SetResult();

            valueTask.GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetResultThrowsTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            valueTaskCompletionSource.SetException(new CustomException());

            Assert.ThrowsException<CustomException>(() =>
            {
                valueTask.GetAwaiter().GetResult();
            });
        }

        [TestMethod]
        public void GetResultAfterContinuationTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;

            Task.Run(async () =>
            {
                await Task.Delay(20);
                valueTaskCompletionSource.SetResult();
            });

            valueTask.GetAwaiter().OnCompleted(() => { });
            valueTask.GetAwaiter().GetResult();
        }

        [TestMethod]
        public void ContinuationAfterGetResultThrowsTest()
        {
            var valueTaskCompletionSource = ValueTaskCompletionSource.Create();
            var valueTask = valueTaskCompletionSource.Task;
            valueTaskCompletionSource.SetResult();

            valueTask.GetAwaiter().GetResult();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                valueTask.GetAwaiter().OnCompleted(() => { });
            });
        }
    }
}
