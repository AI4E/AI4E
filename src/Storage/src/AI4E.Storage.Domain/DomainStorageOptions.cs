/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Options to configure the domain storage.
    /// </summary>
    public class DomainStorageOptions
    {
        private TimeSpan _initalDispatchFailureDelay;
        private TimeSpan _maxDispatchFailureDelay;

        /// <summary>
        /// Creates a new instance of the <see cref="DomainStorageOptions"/> type with a default scope.
        /// </summary>
        public DomainStorageOptions()
        {
            Scope = Assembly.GetEntryAssembly()?.GetName().Name;
            InitalDispatchFailureDelay = TimeSpan.FromMilliseconds(500);
            MaxDispatchFailureDelay = TimeSpan.FromMilliseconds(5000);
        }

        /// <summary>
        /// Gets or sets the scope of the storage engine or <c>null</c> to indicate no scoping.
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// Gets or a boolean value indicating whether the domain storage shall await all domain-events being
        /// dispatched successfully on commit.
        /// </summary>
        public bool WaitForEventsDispatch { get; set; } = false; // TODO: Rename to: SynchronousEventDispatch

        /// <summary>
        /// Gets the initial delay for retrying dispatching domain-events on failure.
        /// </summary>
        public TimeSpan InitalDispatchFailureDelay
        {
            get => _initalDispatchFailureDelay;
            set
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(Resources.TimeSpanMustNotBeNegative);

                _initalDispatchFailureDelay = value;
            }
        }

        /// <summary>
        /// Gets the maximum delay for retrying dispatching domain-events on failure.
        /// </summary>
        public TimeSpan MaxDispatchFailureDelay
        {
            get => _maxDispatchFailureDelay;
            set
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(Resources.TimeSpanMustNotBeNegative);

                _maxDispatchFailureDelay = value;
            }
        }
    }
}
