using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AI4E.Routing
{
    internal static class EscapeHelper
    {
        private static readonly string[] _seperatorStrings = { "/", "\\" };
        private static readonly char[] _seperatorChars = { '/', '\\' };
        private const char _escapeChar = '-';
        private const string _escapeString = "-";

        private const string _escapedEscapeString = "--";
        private const string _escapedSeperatorString = "-/";

        public static void Escape(StringBuilder str, int startIndex)
        {
            // Replace all occurances of - with --
            str.Replace(_escapeString, _escapedEscapeString, startIndex, str.Length - startIndex);

            // Replace all occurances of / and \ with -/
            foreach (var seperator in _seperatorStrings)
            {
                str.Replace(seperator, _escapedSeperatorString, startIndex, str.Length - startIndex);
            }
        }

        public static int CountCharsToEscape(IEnumerable<char> str)
        {
            return str.Count(p => _seperatorChars.Contains(p) || p == _escapeChar);
        }
    }
}
