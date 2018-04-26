using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AI4E.Routing
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
        private const string _escapedSlashString = "-/";
        private const string _escapedBackslashString = "-\\";

        public static void Escape(StringBuilder str, int startIndex)
        {
            // Replace all occurances of - with --
            str.Replace(_escapeString, _escapedEscapeString, startIndex, str.Length - startIndex);

            // Replace all occurances of / with -/
            str.Replace(_slashString, _escapedSlashString, startIndex, str.Length - startIndex);

            // Replace all occurances of \ with -\
            str.Replace(_backSlashString, _escapedBackslashString, startIndex, str.Length - startIndex);
        }

        public static void Unescape(StringBuilder str, int startIndex)
        {
            // Replace all occurances of -- with -
            str.Replace(_escapedEscapeString, _escapeString, startIndex, str.Length - startIndex);

            // Replace all occurances of -/ with /
            str.Replace(_escapedSlashString, _slashString, startIndex, str.Length - startIndex);

            // Replace all occurances of -\ with \
            str.Replace(_escapedBackslashString, _backSlashString, startIndex, str.Length - startIndex);
        }

        public static int CountCharsToEscape(IEnumerable<char> str)
        {
            return str.Count(p => p == _slashChar || p == _backSlashChar || p == _escapeChar);
        }
    }
}
