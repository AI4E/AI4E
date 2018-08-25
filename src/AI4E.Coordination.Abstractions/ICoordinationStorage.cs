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

namespace AI4E.Coordination
{
    public interface ICoordinationStorage
    {
        Task<IStoredEntry> GetEntryAsync(CoordinationEntryPath path, CancellationToken cancellation);
        Task<IStoredEntry> UpdateEntryAsync(IStoredEntry value, IStoredEntry comparand, CancellationToken cancellation);
    }

    public static class CoordinationStorageExtension
    {
        [Obsolete("Use GetEntryAsync(CoordinationEntryPath, CancellationToken)")]
        public static Task<IStoredEntry> GetEntryAsync(this ICoordinationStorage coordinationStorage, string path, CancellationToken cancellation)
        {
            if (coordinationStorage == null)
                throw new ArgumentNullException(nameof(coordinationStorage));

            return coordinationStorage.GetEntryAsync(CoordinationEntryPath.FromEscapedPath( path.AsMemory()),
                                                     cancellation);
        }
    }
}