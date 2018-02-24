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

using System.Transactions;

namespace AI4E.Storage
{
    public sealed class StorageOptions
    {
        public TransactionScopeOption TransactionScopeOption { get; set; } = TransactionScopeOption.Suppress;

        public bool EnableCaching { get; set; } = true;

        public int MaxCacheEntries { get; set; } = 100;

        public int SnapshotInterval { get; set; } = 60 * 60 * 1000;

        public int SnapshotRevisionThreshold { get; set; } = 20;
    }
}
