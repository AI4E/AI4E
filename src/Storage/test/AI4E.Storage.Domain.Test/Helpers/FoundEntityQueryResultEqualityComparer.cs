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
using System.Diagnostics;
using AI4E.Utils;

namespace AI4E.Storage.Domain.Test.Helpers
{
    internal sealed class FoundEntityQueryResultEqualityComparer : IEqualityComparer<FoundEntityQueryResult>
    {
        // TODO: This is a 90% Copy of NotFoundEntityQueryResultEqualityComparer. Use some code generation mechanism...

        private readonly FoundEntityQueryResultEquality _equalityOptions;

        public FoundEntityQueryResultEqualityComparer(
            FoundEntityQueryResultEquality equalityOptions = FoundEntityQueryResultEquality.All)
        {
            _equalityOptions = equalityOptions;
        }

        public bool Equals(FoundEntityQueryResult? x, FoundEntityQueryResult? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null)
                return false;

            Debug.Assert(y != null);

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.EntityIdentifier)
                && x.EntityIdentifier != y.EntityIdentifier)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.ConcurrencyToken)
                && x.ConcurrencyToken != y.ConcurrencyToken)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.LoadedFromCache)
                && x.LoadedFromCache != y.LoadedFromCache)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Reason)
                && x.Reason != y.Reason)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Revision)
                && x.Revision != y.Revision)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Scope)
                && x.Scope != y.Scope)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Entity)
                && x.Entity != y.Entity)
            {
                return false;
            }

            return true;
        }

        public int GetHashCode(FoundEntityQueryResult obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            var hashCode = new HashCode();

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.EntityIdentifier))
            {
                hashCode.Add(obj.EntityIdentifier);
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.ConcurrencyToken))
            {
                hashCode.Add(obj.ConcurrencyToken);
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.LoadedFromCache))
            {
                hashCode.Add(obj.LoadedFromCache);
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Reason))
            {
                hashCode.Add(obj.Reason);
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Revision))
            {
                hashCode.Add(obj.Revision);
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Scope))
            {
                hashCode.Add(obj.Scope);
            }

            if (_equalityOptions.IncludesFlag(FoundEntityQueryResultEquality.Entity))
            {
                hashCode.Add(obj.Entity);
            }

            return hashCode.ToHashCode();
        }
    }

    [Flags]
    internal enum FoundEntityQueryResultEquality
    {
        None = 0,
        EntityIdentifier = 0x01,
        ConcurrencyToken = 0x02,
        LoadedFromCache = 0x04,
        Reason = 0x08,
        Revision = 0x10,
        Scope = 0x20,
        Entity = 0x40,
        All = 0x7F
    }
}
