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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * AsyncEnumerator (https://github.com/Andrew-Hanlon/AsyncEnumerator)
 * MIT License
 * 
 * Copyright (c) 2017 Andrew Hanlon
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AI4E.Async
{
    public abstract class TaskLikeBase
    {
        public bool IsCompleted { get; protected set; }

        public virtual TaskLikeAwaiter GetAwaiter() => new TaskLikeAwaiter(this);

        internal virtual void SetCompletion() => IsCompleted = true;

        internal abstract void SetException(ExceptionDispatchInfo exception);

        public readonly struct TaskLikeAwaiter : INotifyCompletion
        {
            private readonly TaskLikeBase _task;
            private readonly TaskAwaiter _taskAwaiter;

            internal TaskLikeAwaiter(TaskLikeBase task)
            {
                _task = task;
                _taskAwaiter = new TaskAwaiter();
            }

            public bool IsCompleted => _task.IsCompleted;

            public void GetResult() { }

            public void OnCompleted(Action a) { _taskAwaiter.OnCompleted(a); }
        }
    }
}