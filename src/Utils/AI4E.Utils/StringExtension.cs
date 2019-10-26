/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

using System.Linq;

namespace System
{
    public static class AI4EUtilsStringExtension
    {
        public static bool ContainsWhitespace(this string str)
        {
#pragma warning disable CA1062
            return str.Length == 0 ? false : str.Any(c => char.IsWhiteSpace(c));
#pragma warning restore CA1062
        }

#if NETSTD20
        public static int IndexOf(this string str, char value, StringComparison comparisonType)
        {
#pragma warning disable CA1062
            return str.IndexOf(new string(value, count: 1), comparisonType);
#pragma warning restore CA1062
        }

        public static bool Contains(this string str, string value, StringComparison comparisonType)
        {
#pragma warning disable CA1062
            return str.IndexOf(value, comparisonType) >= 0;
#pragma warning restore CA1062
        }

        public static string Replace(this string str, string oldValue, string? newValue, StringComparison comparisonType)
        {
            if (oldValue is null)
                throw new ArgumentNullException(nameof(oldValue));

            if (oldValue.Length == 0)
                throw new ArgumentException("The argument must not be an empty string.", nameof(oldValue));

            if (newValue is null)
                throw new ArgumentNullException(nameof(newValue));

#pragma warning disable CA1062
            var index = str.IndexOf(oldValue, comparisonType);
#pragma warning restore CA1062

            while (index > 0)
            {
                var newStr = string.Empty;

                if (index > 0)
                {
                    newStr = str.Substring(0, index);
                }

                if (!string.IsNullOrEmpty(newValue))
                {
                    newStr += newValue;
                }

                if (index + oldValue.Length < str.Length)
                {
                    newStr += str.Substring(index + oldValue.Length);
                }

                str = newStr;
                index = str.IndexOf(oldValue, comparisonType);
            }

            return str;
        }

        public static int GetHashCode(this string str, StringComparison comparisonType)
        {
            var comparer = comparisonType switch
            {
                StringComparison.Ordinal => StringComparer.Ordinal,
                StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase,
                StringComparison.InvariantCulture => StringComparer.InvariantCulture,
                StringComparison.InvariantCultureIgnoreCase => StringComparer.InvariantCultureIgnoreCase,
                StringComparison.CurrentCulture => StringComparer.CurrentCulture,
                StringComparison.CurrentCultureIgnoreCase => StringComparer.CurrentCultureIgnoreCase,
                _ => null
            };

            if (comparer is null)
            {
#pragma warning disable CA1062
                return str.GetHashCode();
#pragma warning restore CA1062
            }

            if (str is null)
                throw new NullReferenceException();

            return comparer.GetHashCode(str);
        }

#endif
    }
}
