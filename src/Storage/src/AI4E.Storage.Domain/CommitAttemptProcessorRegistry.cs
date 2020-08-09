/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using System.Collections.Generic;
using System.Threading;

namespace AI4E.Storage.Domain
{
    public sealed class CommitAttemptProcessorRegistry : ICommitAttemptProcessorRegistry
    {
        private readonly List<ICommitAttemptProcessorRegistration> _registrations
            = new List<ICommitAttemptProcessorRegistration>();

        private readonly object _mutex = new object();
        private readonly IEntityStorageEngine _storageEngine;
        private ICommitAttemptProccesingQueue? _compiledQueue = null;

        public CommitAttemptProcessorRegistry(IEntityStorageEngine storageEngine)
        {
            if (storageEngine is null)
                throw new ArgumentNullException(nameof(storageEngine));

            _storageEngine = storageEngine;
        }

        public bool Register(ICommitAttemptProcessorRegistration processorRegistration)
        {
            lock (_mutex)
            {
                var result = !_registrations.Contains(processorRegistration);

                if (result)
                {
                    _registrations.Insert(0, processorRegistration);
                    _compiledQueue = null;
                }

                return result;
            }
        }

        public bool Unregister(ICommitAttemptProcessorRegistration processorRegistration)
        {
            lock (_mutex)
            {
                var result = _registrations.Remove(processorRegistration);

                if(result)
                {
                    _compiledQueue = null;
                }

                return result;
            }
        }

        public ICommitAttemptProccesingQueue BuildProcessingQueue()
        {
            var result = Volatile.Read(ref _compiledQueue);

            if (result is null)
            {
                lock (_mutex)
                {
                    result = _compiledQueue;

                    if (result is null)
                    {
                        result = new CommitAttemptProcessingQueue(_storageEngine, _registrations);
                    }
                }
            }

            return result;
        }
    }
}
