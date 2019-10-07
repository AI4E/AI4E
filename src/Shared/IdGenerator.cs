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

using System;
using System.Text;

namespace AI4E.Internal
{
    internal static class IdGenerator
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
