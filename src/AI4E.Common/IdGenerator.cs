using System;
using System.Collections.Generic;
using System.Text;

namespace AI4E
{
    public static class IdGenerator
    {
        private const char _separator = '°';
        private const string _separatorAsString = "°";

        public static string GenerateId(params object[] parts)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));

            var resultBuilder = new StringBuilder();

            foreach (var part in parts)
            {
                if (part == null)
                    continue;

                var idPart = part.ToString();

                if (string.IsNullOrEmpty(idPart))
                    continue;

                if (resultBuilder.Length > 0)
                    resultBuilder.Append(_separator);

                var index = resultBuilder.Length;

                resultBuilder.Append(idPart);
                resultBuilder.Replace(_separatorAsString, _separatorAsString + _separatorAsString, index, idPart.Length);
            }

            return resultBuilder.ToString();
        }
    }
}
