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

using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Compression
{
    // TODO: Fix XML-comments
    public static class AI4EUtilsZipArchiveExtension
    {
        /// <summary>
        /// Extracts all of the files in the archive to a directory on the file system. The specified directory may already exist.
        /// This method will create all subdirectories and the specified directory if necessary.
        /// If there is an error while extracting the archive, the archive will remain partially extracted.
        /// Each entry will be extracted such that the extracted file has the same relative path to destinationDirectoryName as the
        /// entry has to the root of the archive. If a file to be archived has an invalid last modified time, the first datetime
        /// representable in the Zip timestamp format (midnight on January 1, 1980) will be used.
        /// </summary>
        /// 
        /// <exception cref="ArgumentException">destinationDirectoryName is a zero-length string, contains only whitespace,
        /// or contains one or more invalid characters as defined by InvalidPathChars.</exception>
        /// <exception cref="ArgumentNullException">destinationDirectoryName is null.</exception>
        /// <exception cref="PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length.
        /// For example, on Windows-based platforms, paths must be less than 248 characters, and file names must be less than 260 characters.</exception>
        /// <exception cref="DirectoryNotFoundException">The specified path is invalid, (for example, it is on an unmapped drive).</exception>
        /// <exception cref="IOException">An archive entry?s name is zero-length, contains only whitespace, or contains one or more invalid
        /// characters as defined by InvalidPathChars. -or- Extracting an archive entry would have resulted in a destination
        /// file that is outside destinationDirectoryName (for example, if the entry name contains parent directory accessors).
        /// -or- An archive entry has the same name as an already extracted entry from the same archive.</exception>
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission.</exception>
        /// <exception cref="NotSupportedException">destinationDirectoryName is in an invalid format. </exception>
        /// <exception cref="InvalidDataException">An archive entry was not found or was corrupt.
        /// -or- An archive entry has been compressed using a compression method that is not supported.</exception>
        /// 
        /// <param name="destinationDirectoryName">The path to the directory on the file system.
        /// The directory specified must not exist. The path is permitted to specify relative or absolute path information.
        /// Relative path information is interpreted as relative to the current working directory.</param>
        public static Task ExtractToDirectoryAsync(this ZipArchive source, string destinationDirectoryName, CancellationToken cancellation)
        {
            return ExtractToDirectoryAsync(source, destinationDirectoryName, overwrite: false, cancellation);
        }

        /// <summary>
        /// Extracts all of the files in the archive to a directory on the file system. The specified directory may already exist.
        /// This method will create all subdirectories and the specified directory if necessary.
        /// If there is an error while extracting the archive, the archive will remain partially extracted.
        /// Each entry will be extracted such that the extracted file has the same relative path to destinationDirectoryName as the
        /// entry has to the root of the archive. If a file to be archived has an invalid last modified time, the first datetime
        /// representable in the Zip timestamp format (midnight on January 1, 1980) will be used.
        /// </summary>
        /// 
        /// <exception cref="ArgumentException">destinationDirectoryName is a zero-length string, contains only whitespace,
        /// or contains one or more invalid characters as defined by InvalidPathChars.</exception>
        /// <exception cref="ArgumentNullException">destinationDirectoryName is null.</exception>
        /// <exception cref="PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length.
        /// For example, on Windows-based platforms, paths must be less than 248 characters, and file names must be less than 260 characters.</exception>
        /// <exception cref="DirectoryNotFoundException">The specified path is invalid, (for example, it is on an unmapped drive).</exception>
        /// <exception cref="IOException">An archive entry?s name is zero-length, contains only whitespace, or contains one or more invalid
        /// characters as defined by InvalidPathChars. -or- Extracting an archive entry would have resulted in a destination
        /// file that is outside destinationDirectoryName (for example, if the entry name contains parent directory accessors).
        /// -or- An archive entry has the same name as an already extracted entry from the same archive.</exception>
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission.</exception>
        /// <exception cref="NotSupportedException">destinationDirectoryName is in an invalid format. </exception>
        /// <exception cref="InvalidDataException">An archive entry was not found or was corrupt.
        /// -or- An archive entry has been compressed using a compression method that is not supported.</exception>
        /// 
        /// <param name="destinationDirectoryName">The path to the directory on the file system.
        /// The directory specified must not exist. The path is permitted to specify relative or absolute path information.
        /// Relative path information is interpreted as relative to the current working directory.</param>
        /// <param name="overwrite">True to indicate overwrite.</param>
        public static async Task ExtractToDirectoryAsync(
            this ZipArchive source,
            string destinationDirectoryName,
            bool overwrite,
            CancellationToken cancellation)
        {
            if (destinationDirectoryName == null)
                throw new ArgumentNullException(nameof(destinationDirectoryName));

#pragma warning disable CA1062
            foreach (var entry in source.Entries)
#pragma warning restore CA1062
            {
                await entry
                    .ExtractRelativeToDirectoryAsync(destinationDirectoryName, overwrite, cancellation)
                    .ConfigureAwait(false);
            }
        }

        public static async Task AddFileAsEntryAsync(
            this ZipArchive archive,
            string entryName,
            CompressionLevel compressionLevel,
            string filePath,
            CancellationToken cancellation)
        {
#pragma warning disable CA1062
            var entry = archive.CreateEntry(entryName, compressionLevel);
#pragma warning restore CA1062

            using var entryStream = entry.Open();
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);
            await fileStream.CopyToAsync(entryStream, bufferSize: 4096, cancellation).ConfigureAwait(false);
        }

        public static async Task AddFileAsEntryAsync(
            this ZipArchive archive,
            string entryName,
            string filePath,
            CancellationToken cancellation)
        {
#pragma warning disable CA1062
            var entry = archive.CreateEntry(entryName);
#pragma warning restore CA1062

            using var entryStream = entry.Open();
            using var fileStream = new FileStream(
                filePath, FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);
            await fileStream.CopyToAsync(entryStream, bufferSize: 4096, cancellation).ConfigureAwait(false);
        }
    }
}
