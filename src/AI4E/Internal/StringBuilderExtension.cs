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

using System.Text;

namespace AI4E.Internal
{
    // Adapted from: https://stackoverflow.com/questions/1359948/why-doesnt-stringbuilder-have-indexof-method
    internal static class StringBuilderExtension
    {
        public static int IndexOf(this StringBuilder sb, string value, int startIndex, bool ignoreCase)
        {
            var length = value.Length;
            var maxSearchLength = (sb.Length - length) + 1;

            for (var i = startIndex; i < maxSearchLength; ++i)
            {
                if (AreEqual(sb[i], value[0], ignoreCase))
                {
                    var index = 1;
                    for (; index < length && AreEqual(sb[i + index], value[index], ignoreCase); index++) ;

                    if (index == length)
                        return i;
                }
            }

            return -1;
        }


        public static int IndexOf(this StringBuilder sb, char value, int startIndex, bool ignoreCase)
        {
            for (var i = startIndex; i < sb.Length; ++i)
            {
                if (AreEqual(sb[i], value, ignoreCase))
                    return i;
            }

            return -1;
        }

        public static int LastIndexOf(this StringBuilder sb, string value, int startIndex, bool ignoreCase)
        {
            var length = value.Length;
            var maxSearchLength = sb.Length - length;

            for (var i = maxSearchLength; i >= startIndex; --i)
            {
                if (AreEqual(sb[i], value[0], ignoreCase))
                {
                    var index = 1;
                    for (; index < length && AreEqual(sb[i + index], value[index], ignoreCase); index++) ;

                    if (index == length)
                        return i;
                }
            }

            return -1;
        }

        public static int LastIndexOf(this StringBuilder sb, char value, int startIndex, bool ignoreCase)
        {
            for (var i = sb.Length - 1; i >= startIndex; --i)
            {
                if (AreEqual(sb[i], value, ignoreCase))
                    return i;
            }

            return -1;
        }

        private static bool AreEqual(char c1, char c2, bool ignoreCase)
        {
            if (ignoreCase)
            {
                return char.ToLower(c1) == char.ToLower(c2);
            }

            return c1 == c2;
        }
    }
}
