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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Mocks
{
    public sealed class MessageDispatcherMock : IMessageDispatcher
    {
        private readonly List<RecordedMessage> _recordedMessages = new List<RecordedMessage>();
        private readonly object _mutex = new object();

        public async ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation = default)
        {
            var recordedMessage = new RecordedMessage(dispatchData, publish, cancellation);
            var dispatchTask = recordedMessage.DispatchTask;

            lock (_mutex)
            {
                _recordedMessages.Add(recordedMessage);
            }

            var result = await dispatchTask;

            lock (_mutex)
            {
                _recordedMessages.Remove(recordedMessage);
            }

            return result;
        }
    }

    public readonly struct RecordedMessage
    {
        private readonly TaskCompletionSource<IDispatchResult> _dispatchCompletionSource;

        public RecordedMessage(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation)
        {
            DispatchData = dispatchData;
            Publish = publish;
            Cancellation = cancellation;
            _dispatchCompletionSource = new TaskCompletionSource<IDispatchResult>();
        }

        public DispatchDataDictionary DispatchData { get; }
        public bool Publish { get; }
        public CancellationToken Cancellation { get; }

        public bool Handle(IDispatchResult result)
        {
            return _dispatchCompletionSource.TrySetResult(result);
        }

        public Task<IDispatchResult> DispatchTask => _dispatchCompletionSource.Task;
    }
}