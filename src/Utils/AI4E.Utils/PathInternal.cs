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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * corefx (https://github.com/dotnet/corefx)
 * Licensed to the .NET Foundation under one or more agreements.
 * The .NET Foundation licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AI4E.Utils
{
    internal static partial class PathInternal
    {
        /// <summary>Returns a comparison that can be used to compare file and directory names for equality.</summary>
        internal static StringComparison StringComparison => IsCaseSensitive ?
                                                             StringComparison.Ordinal :
                                                             StringComparison.OrdinalIgnoreCase;

        /// <summary>Gets whether the system is case-sensitive.</summary>
        internal static bool IsCaseSensitive { get; } = GetIsCaseSensitive();

        /// <summary>
        /// Determines whether the file system is case sensitive.
        /// </summary>
        /// <remarks>
        /// Ideally we'd use something like pathconf with _PC_CASE_SENSITIVE, but that is non-portable, 
        /// not supported on Windows or Linux, etc. For now, this function creates a tmp file with capital letters 
        /// and then tests for its existence with lower-case letters.  This could return invalid results in corner 
        /// cases where, for example, different file systems are mounted with differing sensitivities.
        /// </remarks>
        private static bool GetIsCaseSensitive()
        {
            try
            {
                var pathWithUpperCase = Path.Combine(
                    Path.GetTempPath(),
                    "CASESENSITIVETEST" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
                using var _ = new FileStream(
                    pathWithUpperCase,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    0x1000,
                    FileOptions.DeleteOnClose);

#pragma warning disable CA1308
                var lowerCased = pathWithUpperCase.ToLowerInvariant();
#pragma warning restore CA1308
                return !File.Exists(lowerCased);

            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                // In case something goes terribly wrong, we don't want to fail just because
                // of a casing test, so we assume case-insensitive-but-preserving.
                Debug.Fail("Casing test failed: " + exc);
                return false;
            }
        }
    }
}

