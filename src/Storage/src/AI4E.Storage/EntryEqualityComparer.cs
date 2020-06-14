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

            if (idMember == null)
            {
                return EqualityComparer<TEntry>.Default.Equals;
            }

            var idType = DataPropertyHelper.GetIdType<TEntry>();
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
