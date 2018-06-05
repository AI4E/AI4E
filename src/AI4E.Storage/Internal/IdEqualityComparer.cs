using System;
using System.Collections.Generic;

namespace AI4E.Storage.Internal
{
    public sealed class IdEqualityComparer<TId> : IEqualityComparer<TId>
    {
        private readonly Func<TId, TId, bool> _idEquality;

        public IdEqualityComparer()
        {
            _idEquality = DataPropertyHelper.BuildIdEquality<TId>().Compile();
        }

        public bool Equals(TId x, TId y)
        {
            return _idEquality(x, y);
        }

        public int GetHashCode(TId obj)
        {
            return obj.GetHashCode();
        }
    }
}
