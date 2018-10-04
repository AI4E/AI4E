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



        private static readonly CoordinationEntryPath _rootPath = new CoordinationEntryPath("modules");
        private static readonly CoordinationEntryPath _rootPrefixesPath = _rootPath.GetChildPath("prefixes"); // prefix => end-point
        private static readonly CoordinationEntryPath _rootRunningPath = _rootPath.GetChildPath("running"); // module => (prefixes, end-point)

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

                        var routeBytes = Encoding.UTF8.GetBytes(endPoint.Route);

                        using (var stream = new MemoryStream(capacity: 4 + routeBytes.Length))
                        {
                            using (var writer2 = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                            {
                                writer2.Write(routeBytes.Length);
                                writer2.Write(routeBytes);
                            }

                            var payload = stream.ToArray();
                            var entry = await _coordinationManager.GetOrCreateAsync(prefixPath, payload, EntryCreationModes.Ephemeral, cancellation);

                            Assert(entry.Value.SequenceEqual(payload));
                        }
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

            using (var stream = entry.OpenStream())
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

            var result = new List<EndPointRoute>(capacity: entry.Children.Count);

            var childEntries = (await entry.GetChildrenEntriesAsync(cancellation)).OrderBy(p => p.CreationTime);

            foreach (var childEntry in childEntries)
            {
                using (var stream = childEntry.OpenStream())
                using (var reader = new BinaryReader(stream))
                {
                    var routeBytesLength = reader.ReadInt32();
                    var routeBytes = reader.ReadBytes(routeBytesLength);
                    result.Add(new EndPointRoute(Encoding.UTF8.GetString(routeBytes)));
                }
            }

            return result;
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

                using (var stream = sessionEntry.OpenStream())
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

        private static CoordinationEntryPath GetPrefixPath(string prefix, bool normalize = true)
        {
            if (normalize)
                prefix = NormalizePrefix(prefix);

            return _rootPrefixesPath.GetChildPath(prefix);
        }

        private static CoordinationEntryPath GetPrefixPath(string prefix, EndPointRoute endPoint, Session session, bool normalize = true)
        {
            if (normalize)
                prefix = NormalizePrefix(prefix);

            var uniqueEntryName = IdGenerator.GenerateId(endPoint.Route, session.ToString());
            return _rootPrefixesPath.GetChildPath(prefix, uniqueEntryName);
        }

        private static CoordinationEntryPath GetRunningModulePath(ModuleIdentifier module)
        {
            return _rootRunningPath.GetChildPath(module.Name);
        }

        private static CoordinationEntryPath GetRunningModulePath(ModuleIdentifier module, Session session)
        {
            return _rootRunningPath.GetChildPath(module.Name, session.ToString());
        }
    }
}
