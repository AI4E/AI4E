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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Routing;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity
{
    public sealed class RunningModuleLookup : IRunningModuleLookup
    {
        private static readonly byte[] _emptyPayload = new byte[0];
        private const string _whitespaceRegexPattern = @"\s+";
        private static readonly Regex _whitespaceRegex = new Regex(_whitespaceRegexPattern, RegexOptions.CultureInvariant |
                                                                                            RegexOptions.Singleline |
                                                                                            RegexOptions.IgnoreCase |
                                                                                            RegexOptions.Compiled);
        private const string _rootPath = "/modules";
        private const string _rootPrefixesPath = _rootPath + "/prefixes"; // prefix => end-point
        private const string _rootRunningPath = _rootPath + "/running"; // module => (prefixes, end-point)

        private readonly ICoordinationManager _coordinationManager;

        #region C'tor

        public RunningModuleLookup(ICoordinationManager coordinationManager)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
        }

        #endregion

        #region IRunningModuleLookup

        public async Task AddModuleAsync(ModuleIdentifier module, EndPointRoute endPoint, IEnumerable<string> prefixes, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (prefixes == null)
                throw new ArgumentNullException(nameof(prefixes));

            if (!prefixes.Any())
                throw new ArgumentException("The collection must not be empty.", nameof(prefixes));

            if (prefixes.Any(p => string.IsNullOrWhiteSpace(p)))
                throw new ArgumentException("The collection must not contain null entries or entries that are empty or contain whitespace only.", nameof(prefixes));

            var prefixList = (prefixes as ICollection<string>) ?? prefixes.ToList();
            var session = await _coordinationManager.GetSessionAsync(cancellation);

            byte[] runningModulePayload;

            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memoryStream))
                {
                    var endPointAddress = endPoint.Route;
                    var endPointAddressBytes = Encoding.UTF8.GetBytes(endPointAddress);

                    writer.Write(endPointAddressBytes.Length);
                    writer.Write(endPointAddressBytes);
                    writer.Write(prefixes.Count());

                    foreach (var prefix in prefixes)
                    {
                        var normalizedPrefix = NormalizePrefix(prefix);
                        var normalizedPrefixBytes = Encoding.UTF8.GetBytes(normalizedPrefix);
                        var prefixPath = GetPrefixPath(normalizedPrefix, endPoint, session, normalize: false);
                        writer.Write(normalizedPrefixBytes.Length);
                        writer.Write(normalizedPrefixBytes);

                        await _coordinationManager.GetOrCreateAsync(prefixPath, _emptyPayload, EntryCreationModes.Ephemeral, cancellation);
                    }
                }

                runningModulePayload = memoryStream.ToArray();
            }

            var runningModulePath = GetRunningModulePath(module, session);

            await _coordinationManager.GetOrCreateAsync(runningModulePath, runningModulePayload, EntryCreationModes.Ephemeral, cancellation);
        }

        public async Task RemoveModuleAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var runningModulePath = GetRunningModulePath(module, session);

            var entry = await _coordinationManager.GetAsync(runningModulePath, cancellation);

            if (entry == null)
                return;

            await _coordinationManager.DeleteAsync(runningModulePath, cancellation: cancellation);

            using (var stream = new MemoryStream(entry.Value.ToArray()))
            using (var reader = new BinaryReader(stream))
            {
                var endPointAddressBytesLength = reader.ReadInt32();
                var endPointAddressBytes = reader.ReadBytes(endPointAddressBytesLength);
                var endPointAddress = Encoding.UTF8.GetString(endPointAddressBytes);
                var endPoint = EndPointRoute.CreateRoute(endPointAddress);
                var prefixesCount = reader.ReadInt32();

                for (var i = 0; i < prefixesCount; i++)
                {
                    var prefixBytesLength = reader.ReadInt32();
                    var prefixBytes = reader.ReadBytes(prefixBytesLength);
                    var prefix = Encoding.UTF8.GetString(prefixBytes);
                    var prefixPath = GetPrefixPath(prefix, endPoint, session, normalize: false);

                    await _coordinationManager.DeleteAsync(prefixPath, cancellation: cancellation);
                }
            }
        }

        public async ValueTask<IEnumerable<EndPointRoute>> GetEndPointsAsync(string prefix, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentNullOrWhiteSpaceException(nameof(prefix));

            var normalizedPrefix = NormalizePrefix(prefix);

            // It is not possible to register a route for the root path.
            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                return Enumerable.Empty<EndPointRoute>();
            }

            var path = GetPrefixPath(normalizedPrefix, normalize: false);
            var entry = await _coordinationManager.GetOrCreateAsync(path, _emptyPayload, EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            return await entry.GetChildrenEntries().OrderBy(p => p.CreationTime).Select(p => EndPointRoute.CreateRoute(EntryPathHelper.ExtractRoute(p.Path))).ToList();
        }

        public async ValueTask<IEnumerable<string>> GetPrefixesAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            var runningModulePath = GetRunningModulePath(module);

            var entry = await _coordinationManager.GetAsync(runningModulePath, cancellation);

            if (entry == null)
                return Enumerable.Empty<string>();

            IEnumerable<string> GetPrefixes(IEntry sessionEntry)
            {
                string[] result;

                using (var stream = new MemoryStream(sessionEntry.Value.ToArray()))
                using (var reader = new BinaryReader(stream))
                {
                    var bytesToSkip = reader.ReadInt32();
                    for (var i = 0; i < bytesToSkip; i++)
                    {
                        reader.ReadByte();
                    }

                    var prefixesCount = reader.ReadInt32();
                    result = new string[prefixesCount];

                    for (var i = 0; i < prefixesCount; i++)
                    {
                        var prefixBytesLength = reader.ReadInt32();
                        var prefixBytes = reader.ReadBytes(prefixBytesLength);
                        result[i] = Encoding.UTF8.GetString(prefixBytes);
                    }
                }

                return result;
            }

            return await entry.GetChildrenEntries().SelectMany(p => GetPrefixes(p).ToAsyncEnumerable()).Distinct().ToList();
        }

        #endregion

        private static string NormalizePrefix(string prefix)
        {
            prefix = _whitespaceRegex.Replace(prefix, "");

            if (prefix.StartsWith("/"))
            {
                prefix = prefix.Substring(1);
            }

            return prefix;
        }

        private static string GetPrefixPath(string prefix, bool normalize = true)
        {
            if (normalize)
                prefix = NormalizePrefix(prefix);

            var escapedPrefix = Escape(prefix);

            return EntryPathHelper.GetChildPath(_rootPrefixesPath, escapedPrefix, normalize: false);
        }

        private static string GetPrefixPath(string prefix, EndPointRoute endPoint, string session, bool normalize = true)
        {
            return EntryPathHelper.GetChildPath(GetPrefixPath(prefix, normalize), EntryPathHelper.GetEntryName(endPoint.Route, session));
        }

        private static string GetRunningModulePath(ModuleIdentifier module)
        {
            var moduleName = module.Name;
            var escapedModuleName = Escape(moduleName);

            return EntryPathHelper.GetChildPath(_rootRunningPath, escapedModuleName, normalize: false);
        }

        private static string GetRunningModulePath(ModuleIdentifier module, string session)
        {
            return EntryPathHelper.GetChildPath(GetRunningModulePath(module), session);
        }

        private static string Escape(string str)
        {
            var escapedResultBuilder = new StringBuilder(str.Length + EscapeHelper.CountCharsToEscape(str));
            escapedResultBuilder.Append(str);
            EscapeHelper.Escape(escapedResultBuilder, startIndex: 0);
            var result = escapedResultBuilder.ToString();
            return result;
        }
    }
}
