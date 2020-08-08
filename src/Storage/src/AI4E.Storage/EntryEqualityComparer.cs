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
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Internal;

namespace AI4E.Storage
{
    public sealed class EntryEqualityComparer<TEntry> : IEqualityComparer<TEntry>
            where TEntry : class
    {
        private readonly Func<TEntry, TEntry, bool> _equality;

        public static EntryEqualityComparer<TEntry> Instance { get; } = new EntryEqualityComparer<TEntry>();

        private EntryEqualityComparer()
        {
            _equality = BuildEquality();
        }

        private static Func<TEntry, TEntry, bool> BuildEquality()
        {
            var idMember = DataPropertyHelper.GetIdMember<TEntry>();
            var idType = DataPropertyHelper.GetIdType<TEntry>();

            if (idMember == null || idType is null)
            {
                return EqualityComparer<TEntry>.Default.Equals;
            }

            var x = Expression.Parameter(typeof(TEntry), "x");
            var y = Expression.Parameter(typeof(TEntry), "y");

            Expression idAccessY, idAccessX;

            if (idMember.MemberType == MemberTypes.Method)
            {
                idAccessX = Expression.Call(x, (MethodInfo)idMember);
                idAccessY = Expression.Call(y, (MethodInfo)idMember);

            }
            else if (idMember.MemberType == MemberTypes.Field || idMember.MemberType == MemberTypes.Property)
            {
                idAccessX = Expression.MakeMemberAccess(x, idMember);
                idAccessY = Expression.MakeMemberAccess(y, idMember);
            }
            else
            {
                return EqualityComparer<TEntry>.Default.Equals;
            }

            var equalityExpression = DataPropertyHelper.BuildIdEqualityExpression(idType, idAccessX, idAccessY);
            return Expression.Lambda<Func<TEntry, TEntry, bool>>(equalityExpression, x, y).Compile();
        }

        public bool Equals(TEntry? x, TEntry? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return _equality(x, y);
        }

        public int GetHashCode(TEntry obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            if (DataPropertyHelper.TryGetId(typeof(TEntry), obj, out var id))
            {
                return EqualityComparer<object>.Default.GetHashCode(id);
            }

            return EqualityComparer<TEntry>.Default.GetHashCode(obj);
        }
    }
}
