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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;

namespace AI4E.Processing
{
    public sealed class AsyncProcess : IAsyncProcess
    {
        private readonly Func<CancellationToken, Task> _operation;
        private readonly object _lock = new object();

        private Task _execution = Task.CompletedTask;
        private CancellationTokenSource _cancellationSource;

        private TaskCompletionSource<object> _startNotificationSource;
        private TaskCompletionSource<object> _terminationNotificationSource;

        public AsyncProcess(Func<CancellationToken, Task> operation, bool start = false)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            _operation = operation;

            if (start)
            {
                Start();
            }
        }

        public Task Startup => _startNotificationSource?.Task ?? Task.CompletedTask;
        public Task Termination => _terminationNotificationSource?.Task ?? Task.CompletedTask;

        public AsyncProcessState State
        {
            get
            {
                if (_execution.IsRunning())
                    return AsyncProcessState.Running;

                if (_execution.IsCompleted)
                    return AsyncProcessState.Terminated;

                return AsyncProcessState.Failed;
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_execution.IsRunning())
                    return;

                _startNotificationSource = new TaskCompletionSource<object>();
                _terminationNotificationSource = new TaskCompletionSource<object>();
                _cancellationSource = new CancellationTokenSource();
                _execution = Execute();
            }
        }

        public Task StartAsync(CancellationToken cancellation)
        {
            Start();

            var result = _startNotificationSource.Task;

            if (cancellation.CanBeCanceled)
            {
                result = result.WithCancellation(cancellation);
            }

            return result;
        }

        public void Terminate()
        {
            lock (_lock)
            {
                if (!_execution.IsRunning())
                    return;

                _cancellationSource.Cancel();
            }
        }

        public Task TerminateAsync(CancellationToken cancellation)
        {
            Terminate();

            var result = _terminationNotificationSource?.Task ?? Task.CompletedTask;

            if (cancellation.CanBeCanceled)
            {
                result = result.WithCancellation(cancellation);
            }

            return result;
        }

        private async Task Execute()
        {
            try
            {
                var cancellation = _cancellationSource.Token;

                await Task.Yield();

                try
                {
                    _startNotificationSource.SetResult(null);

                    await _operation(cancellation).ConfigureAwait(false);

                    if (!cancellation.IsCancellationRequested)
                    {
                        throw new UnexpectedProcessTerminationException();
                    }
                }
                catch (TaskCanceledException) when (_cancellationSource.IsCancellationRequested) { }
                catch (Exception exc)
                {
                    _terminationNotificationSource.SetException(exc);
                }
            }
            finally
            {
                _terminationNotificationSource.TrySetResult(null);
            }
        }
    }
}
