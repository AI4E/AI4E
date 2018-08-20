using System;
using System.Linq;

namespace AI4E.Internal
{
    public static class StringExtension
    {
        public static bool ContainsWhitespace(this string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                return false;

            return str.Any(c => char.IsWhiteSpace(c));
        }
    }
}
