///* License
// * --------------------------------------------------------------------------------------------------------------------
// * This file is part of the AI4E distribution.
// *   (https://github.com/AI4E/AI4E)
// * Copyright (c) 2018 Andreas Truetschel and contributors.
// * 
// * AI4E is free software: you can redistribute it and/or modify  
// * it under the terms of the GNU Lesser General Public License as   
// * published by the Free Software Foundation, version 3.
// *
// * AI4E is distributed in the hope that it will be useful, but 
// * WITHOUT ANY WARRANTY; without even the implied warranty of 
// * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
// * Lesser General Public License for more details.
// *
// * You should have received a copy of the GNU Lesser General Public License
// * along with this program. If not, see <http://www.gnu.org/licenses/>.
// * --------------------------------------------------------------------------------------------------------------------
// */

//using Nito.AsyncEx;

//namespace AI4E.Coordination
//{
//    /// <summary>
//    /// Represent a cache entry.
//    /// </summary>
//    public interface ICacheEntry
//    {
//        /// <summary>
//        /// The path of the entry, the cache entry stores.
//        /// </summary>
//        CoordinationEntryPath Path { get; }

//        /// <summary>
//        /// A boolean value indicating whether the cache entry is valid and <see cref="Entry"/> can be used safely.
//        /// </summary>
//        bool IsValid { get; }

//        /// <summary>
//        /// The stored entry, the cache entry stored.
//        /// </summary>
//        IStoredEntry Entry { get; }

//        /// <summary>
//        /// The version of the cache entry.
//        /// </summary>
//        int CacheEntryVersion { get; }

//        /// <summary>
//        /// The local lock of the cache entry.
//        /// </summary>
//        AsyncLock LocalLock { get; } // TODO: We have a hard dependency on Nito.AsyncEx here.
//    }
//}
