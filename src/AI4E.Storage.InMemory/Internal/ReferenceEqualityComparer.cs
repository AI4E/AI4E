using System.Collections.Generic;

namespace AI4E.Storage.InMemory.Internal
{
    // Based on: https://github.com/Burtsev-Alexey/net-object-deep-copy/
    internal class ReferenceEqualityComparer : EqualityComparer<object>
    {
        public override bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public override int GetHashCode(object obj)
        {
            if (obj == null)
                return 0;

            return obj.GetHashCode();
        }
    }
}
