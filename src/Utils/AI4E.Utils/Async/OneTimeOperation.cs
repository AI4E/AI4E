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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Async
{
    public sealed class OneTimeOperation
    {
        private readonly Func<Task> _operation;
        private readonly TaskCompletionSource<object?> _executionSource = new TaskCompletionSource<object?>();

        private Task? _executeTask;
        private volatile bool _hasStarted = false;

        public OneTimeOperation(Func<Task> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            _operation = operation;
        }

        public Task ExecuteAsync(CancellationToken cancellation)
        {
            return ExecuteAsync().WithCancellation(cancellation);
        }

        public Task ExecuteAsync()
        {
            Execute();
            return Execution;
        }

        public void Execute()
        {
            if (_hasStarted) // Volatile read op.
                return;

            lock (_executionSource)
            {
                // We use a dedicated flag for specifying whether the operation was already started 
                // instead of simply check _executeTask for beeing set already to allow 
                // recursive calls to Execute() in the executed operation.

                if (_hasStarted)
                    return;

                _hasStarted = true;

                Debug.Assert(_executeTask == null);

                _executeTask = ExecuteInternalAsync();
            }
        }

        private async Task ExecuteInternalAsync()
        {
#if DEBUG
            var executionSourceSetLocally = false;
#endif
            try
            {
                try
                {
                    await _operation().ConfigureAwait(false);
                }
                catch (OperationCanceledException exc)
                {
                    bool successfullySetExecutionSource;

                    if (exc.CancellationToken == default)
                    {
                        successfullySetExecutionSource = _executionSource.TrySetCanceled();
                    }
                    else
                    {
                        successfullySetExecutionSource = _executionSource.TrySetCanceled(exc.CancellationToken);
                    }

#if DEBUG
                    Debug.Assert(successfullySetExecutionSource);
                    executionSourceSetLocally = true;
#endif
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    var successfullySetExecutionSource = _executionSource.TrySetException(exc);

#if DEBUG
                    Debug.Assert(successfullySetExecutionSource);
                    executionSourceSetLocally = true;
#endif
                }
            }
            finally
            {
                var executionSourceSet = _executionSource.TrySetResult(null);
#if DEBUG
                Debug.Assert(executionSourceSet || executionSourceSetLocally);
#endif
            }
        }

        public Task Execution => _executionSource.Task;
    }
}
