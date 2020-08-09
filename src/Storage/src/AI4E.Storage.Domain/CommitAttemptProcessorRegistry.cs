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

// TODO: Does this has to be thread-safe?

using System.Collections.Generic;

namespace AI4E.Storage.Domain
{
    public sealed class CommitAttemptProcessorRegistry : ICommitAttemptProcessorRegistry
    {
        private readonly List<ICommitAttemptProcessorRegistration> _registrations = new List<ICommitAttemptProcessorRegistration>();

        public bool Register(ICommitAttemptProcessorRegistration processorRegistration)
        {
            var result = _registrations.Contains(processorRegistration);

            if (!result)
            {
                _registrations.Add(processorRegistration);
            }

            return !result;
        }

        public bool Unregister(ICommitAttemptProcessorRegistration processorRegistration)
        {
            return _registrations.Remove(processorRegistration);
        }

        public ICommitAttemptProccesingQueue BuildProcessingQueue(IEntityStorageEngine storageEngine)
        {
            return new CommitAttemptProcessingQueue(storageEngine, _registrations);
        }
    }
}
