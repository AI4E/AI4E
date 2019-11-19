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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils
{
    public readonly struct TaskCancellationTokenSource : IDisposable, IEquatable<TaskCancellationTokenSource>
    {
        private readonly CancellationTokenSource? _cancellationTokenSource;
        private readonly Task? _task;

        public TaskCancellationTokenSource(Task task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            _task = task;

            if (task.IsCompleted)
            {
                _cancellationTokenSource = null;
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            task.ContinueWith(
                (_, cts) => ((CancellationTokenSource)cts!).Cancel(),
                _cancellationTokenSource,
                TaskScheduler.Default);
        }

        public TaskCancellationTokenSource(Task task, params CancellationToken[] linkedTokens)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (linkedTokens == null)
                throw new ArgumentNullException(nameof(linkedTokens));

            _task = task;

            if (task.IsCompleted || linkedTokens.Any(p => p.IsCancellationRequested))
            {
                _cancellationTokenSource = null;
                return;
            }

            if (!linkedTokens.Any())
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
            else
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(linkedTokens);
            }

            static void CancelSource(Task _, object obj)
            {
                var cts = (CancellationTokenSource)obj;

                if (cts.IsCancellationRequested)
                    return;

                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            task.ContinueWith(
                CancelSource!,
                _cancellationTokenSource,
                TaskScheduler.Default);
        }

        public Task Task => _task ?? Task.CompletedTask;
        public CancellationToken CancellationToken
        {
            get
            {
                if (_cancellationTokenSource == null)
                {
                    return CreateCanceledToken();
                }

                // Prevent throwing an ObjectDisposedException
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    return CreateCanceledToken();
                }

                try
                {
                    return _cancellationTokenSource.Token;
                }
                catch (ObjectDisposedException)
                {
                    return CreateCanceledToken();
                }
            }
        }

        private static CancellationToken CreateCanceledToken()
        {
            return new CancellationToken(canceled: true);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }

        public bool Equals(TaskCancellationTokenSource other)
        {
            return other._task == _task;
        }

        public override bool Equals(object? obj)
        {
            return obj is TaskCancellationTokenSource taskCancellationTokenSource
                && Equals(taskCancellationTokenSource);
        }

        public override int GetHashCode()
        {
            return _task?.GetHashCode() ?? 0;
        }

        public static bool operator ==(TaskCancellationTokenSource left, TaskCancellationTokenSource right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TaskCancellationTokenSource left, TaskCancellationTokenSource right)
        {
            return !left.Equals(right);
        }


    }
}
