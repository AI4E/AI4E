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

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AI4E.Internal
{
    internal static class EscapeHelper
    {
        private static readonly string _slashString = "/";
        private static readonly char _slashChar = '/';

        private static readonly string _backSlashString = "\\";
        private static readonly char _backSlashChar = '\\';

        private const char _escapeChar = '-';
        private const string _escapeString = "-";

        private const string _escapedEscapeString = "--";
        private const string _escapedSlashString = "-X";
        private const string _escapedBackslashString = "-Y";

        public static void Escape(StringBuilder str, int startIndex)
        {
            // Replace all occurances of - with --
            str.Replace(_escapeString, _escapedEscapeString, startIndex, str.Length - startIndex);

            // Replace all occurances of / with -X
            str.Replace(_slashString, _escapedSlashString, startIndex, str.Length - startIndex);

            // Replace all occurances of \ with -Y
            str.Replace(_backSlashString, _escapedBackslashString, startIndex, str.Length - startIndex);
        }

        public static void Unescape(StringBuilder str, int startIndex)
        {
            // Replace all occurances of -X with /
            str.Replace(_escapedSlashString, _slashString, startIndex, str.Length - startIndex);

            // Replace all occurances of -Y with \
            str.Replace(_escapedBackslashString, _backSlashString, startIndex, str.Length - startIndex);

            // Replace all occurances of -- with -
            // This has to be the last operation as otherwise the unescape operation may unescape none escaped sequences. 
            // F.e. ABC--X => ABC-X => ABC/ (which is incorrect)
            // Correctly unescaped value is ABC-X
            str.Replace(_escapedEscapeString, _escapeString, startIndex, str.Length - startIndex);
        }

        public static int CountCharsToEscape(IEnumerable<char> str)
        {
            return str.Count(p => p == _slashChar || p == _backSlashChar || p == _escapeChar);
        }
    }
}
