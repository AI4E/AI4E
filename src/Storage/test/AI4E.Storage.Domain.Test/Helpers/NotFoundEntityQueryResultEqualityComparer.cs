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
    internal sealed class NotFoundEntityQueryResultEqualityComparer : IEqualityComparer<NotFoundEntityQueryResult>
    {
        private readonly NotFoundEntityQueryResultEquality _equalityOptions;

        public NotFoundEntityQueryResultEqualityComparer(
            NotFoundEntityQueryResultEquality equalityOptions = NotFoundEntityQueryResultEquality.All)
        {
            _equalityOptions = equalityOptions;
        }

        public bool Equals(NotFoundEntityQueryResult? x, NotFoundEntityQueryResult? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null)
                return false;

            Debug.Assert(y != null);

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.EntityIdentifier)
                && x.EntityIdentifier != y.EntityIdentifier)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.ConcurrencyToken)
                && x.ConcurrencyToken != y.ConcurrencyToken)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.LoadedFromCache)
                && x.LoadedFromCache != y.LoadedFromCache)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.Reason)
                && x.Reason != y.Reason)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.Revision)
                && x.Revision != y.Revision)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.Scope)
                && x.Scope != y.Scope)
            {
                return false;
            }

            return true;
        }

        public int GetHashCode(NotFoundEntityQueryResult obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            var hashCode = new HashCode();

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.EntityIdentifier))
            {
                hashCode.Add(obj.EntityIdentifier);
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.ConcurrencyToken))
            {
                hashCode.Add(obj.ConcurrencyToken);
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.LoadedFromCache))
            {
                hashCode.Add(obj.LoadedFromCache);
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.Reason))
            {
                hashCode.Add(obj.Reason);
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.Revision))
            {
                hashCode.Add(obj.Revision);
            }

            if (_equalityOptions.IncludesFlag(NotFoundEntityQueryResultEquality.Scope))
            {
                hashCode.Add(obj.Scope);
            }

            return hashCode.ToHashCode();
        }
    }

    [Flags]
    internal enum NotFoundEntityQueryResultEquality
    {
        None = 0,
        EntityIdentifier = 0x01,
        ConcurrencyToken = 0x02,
        LoadedFromCache = 0x04,
        Reason = 0x08,
        Revision = 0x10,
        Scope = 0x20,
        All = 0x3F
    }
}
