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
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Coordination.Session;
using AI4E.Internal;
using AI4E.Routing;
using AI4E.Utils;
using AI4E.Utils.Memory;
using AI4E.Utils.Memory.Compatibility;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity
{
    public sealed class ModuleManager : IModuleManager
    {
        private static readonly byte[] _emptyPayload = new byte[0];
        private const string _whitespaceRegexPattern = @"\s+";
        private static readonly Regex _whitespaceRegex = new Regex(_whitespaceRegexPattern, RegexOptions.CultureInvariant |
                                                                                            RegexOptions.Singleline |
                                                                                            RegexOptions.IgnoreCase |
                                                                                            RegexOptions.Compiled);

        private static readonly CoordinationEntryPath _rootPath = new CoordinationEntryPath("modules");

        // Used to insert entries that map from a modules prefix to the end-points a module is present at. This is needed for http-dispatch.
        // The storage scheme is: {_rootPrefixesPath}/{prefix}/{end-point session combination}
        // The entry contains the end-point as payload.
        private static readonly CoordinationEntryPath _rootPrefixesPath = _rootPath.GetChildPath("prefixes"); // prefix => end-point

        // Used to insert entries that map from module ids to theit properties (prefixes and end-points).
        // The storage scheme is: {_rootRunningPath}/{module-name}/{session}
        // The entry contains the module properties for the session.
        private static readonly CoordinationEntryPath _rootRunningPath = _rootPath.GetChildPath("running"); // module => (prefixes, end-point)

        private readonly ICoordinationManager _coordinationManager;

        #region C'tor

        public ModuleManager(ICoordinationManager coordinationManager)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
        }

        #endregion

        #region IRunningModuleLookup

        public async Task AddModuleAsync(ModuleIdentifier module, ModuleProperties properties, bool overrideExisting, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var tasks = new List<Task>(capacity: properties.Prefixes.Count * properties.EndPoints.Count);

            foreach (var endPoint in properties.EndPoints)
            {
                foreach (var prefix in properties.Prefixes)
                {
                    tasks.Add(WriteModulePrefixEntryAsync(prefix.AsMemory(), endPoint, session, cancellation));
                }
            }

            await Task.WhenAll(tasks);

            await WriteRunningModuleEntryAsync(module, properties, overrideExisting, session, cancellation);

            // TODO: When cancelled, alls completed operations should be reverted.
            // TODO: The RemoveModuleAsync alogrithm assumes that there are no prefix entries, if the running module entry is not present. We should reflect this assumtion here.
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

            var (endPoints, prefixes) = ReadRunningModuleEntry(entry);

            foreach (var endPoint in endPoints)
            {
                foreach (var prefix in prefixes)
                {
                    var prefixPath = GetPrefixPath(prefix, endPoint, session, normalize: false);
                    await _coordinationManager.DeleteAsync(prefixPath, cancellation: cancellation);
                }
            }
        }

        public async ValueTask<IEnumerable<EndPointAddress>> GetEndPointsAsync(ReadOnlyMemory<char> prefix, CancellationToken cancellation)
        {
            if (prefix.Span.IsEmptyOrWhiteSpace())
                throw new ArgumentException("The argument must not be empty, not consist of whitespace only.", nameof(prefix));

            var normalizedPrefix = NormalizePrefix(prefix);

            // It is not possible to register an end-point address for the root path.
            if (normalizedPrefix.IsEmpty || normalizedPrefix.Span[0] == '_')
            {
                return Enumerable.Empty<EndPointAddress>();
            }

            var path = GetPrefixPath(normalizedPrefix, normalize: false);
            var entry = await _coordinationManager.GetOrCreateAsync(path, _emptyPayload, EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            var result = new List<EndPointAddress>(capacity: entry.Children.Count);
            var childEntries = (await entry.GetChildrenEntriesAsync(cancellation)).OrderBy(p => p.CreationTime).ToList();

            foreach (var childEntry in childEntries)
            {
                var endPoint = ReadModulePrefixEntry(childEntry);
                result.Add(endPoint);
            }

            return result;
        }

        public async ValueTask<ModuleProperties> GetPropertiesAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            var runningModulePath = GetRunningModulePath(module);
            var rootEntry = await _coordinationManager.GetAsync(runningModulePath, cancellation);

            if (rootEntry == null)
                return null;

            var endPointsBuilder = ImmutableList.CreateBuilder<EndPointAddress>();
            var prefixesBuilder = ImmutableList.CreateBuilder<string>();
            var entries = await rootEntry.GetChildrenEntriesAsync(cancellation);

            foreach (var entry in entries)
            {
                var (endPoints, prefixes) = ReadRunningModuleEntry(entry);

                foreach (var endPoint in endPoints)
                {
                    if (!endPointsBuilder.Contains(endPoint))
                    {
                        endPointsBuilder.Add(endPoint);
                    }
                }

                foreach (var prefix in prefixes)
                {
                    if (!prefixesBuilder.Any(p => p.AsSpan().SequenceEqual(prefix.Span)))
                    {
                        prefixesBuilder.Add(prefix.ConvertToString());
                    }
                }
            }

            if (!prefixesBuilder.Any() || !endPointsBuilder.Any())
            {
                // If we there is a module present with this identifier, there must be registered at least one prefix and one end-point.
                Assert(!prefixesBuilder.Any());
                Assert(!endPointsBuilder.Any());

                return null;
            }

            return new ModuleProperties(prefixesBuilder.ToImmutable(), endPointsBuilder.ToImmutable());

        }

        #endregion

        private EndPointAddress ReadModulePrefixEntry(IEntry entry)
        {
            var reader = new BinarySpanReader(entry.Value.Span, ByteOrder.LittleEndian);
            return ReadEndPointAddress(ref reader);
        }

        private (IEnumerable<EndPointAddress> endPoints, IReadOnlyCollection<ReadOnlyMemory<char>> prefixes) ReadRunningModuleEntry(IEntry entry)
        {
            var reader = new BinarySpanReader(entry.Value.Span, ByteOrder.LittleEndian);

            var endPointsCount = reader.ReadInt32();
            var endPoints = new List<EndPointAddress>(capacity: endPointsCount);
            for (var i = 0; i < endPointsCount; i++)
            {
                var endPoint = ReadEndPointAddress(ref reader);
                endPoints.Add(endPoint);
            }

            var prefixesCount = reader.ReadInt32();
            var prefixes = new List<ReadOnlyMemory<char>>(capacity: prefixesCount);

            for (var i = 0; i < prefixesCount; i++)
            {
                var bytes = reader.ReadInt32();
                var prefix =  Encoding.UTF8.GetString(reader.Read(bytes)).AsMemory();
                prefixes.Add(prefix);
            }

            return (endPoints, prefixes);
        }

        private static EndPointAddress ReadEndPointAddress(ref BinarySpanReader reader)
        {
            var localEndPointBytesLenght = reader.ReadInt32();

            if (localEndPointBytesLenght == 0)
            {
                return EndPointAddress.UnknownAddress;
            }

            var utf8EncodedValue = reader.Read(localEndPointBytesLenght);

            return new EndPointAddress(utf8EncodedValue.ToArray());
        }

        private async Task WriteRunningModuleEntryAsync(
            ModuleIdentifier module,
            ModuleProperties properties,
            bool overrideExisting,
            SessionIdentifier session,
            CancellationToken cancellation)
        {
            var path = GetRunningModulePath(module, session);

            IEntry existing;
            byte[] payload;

            async Task<bool> AddOrUpdateAsync()
            {
                if (existing == null)
                {
                    try
                    {
                        await _coordinationManager.CreateAsync(path, payload, EntryCreationModes.Ephemeral, cancellation);
                    }
                    catch (DuplicateEntryException) { return false; }
                }
                else
                {
                    try
                    {
                        var comparand = await _coordinationManager.SetValueAsync(path, payload, version: overrideExisting ? 0 : existing.Version, cancellation);

                        if (!overrideExisting && comparand != existing.Version)
                        {
                            return false;
                        }
                    }
                    catch (EntryNotFoundException) { return false; }
                }

                return true;
            }

            do
            {
                existing = await _coordinationManager.GetAsync(path, cancellation);

                var endPoints = properties.EndPoints.ToHashSet();
                var prefixes = properties.Prefixes.Select(p => p.AsMemory()).ToHashSet();

                if (!overrideExisting)
                {
                    var (existingEndPoints, existingPrefixes) = ReadRunningModuleEntry(existing);
                    endPoints.UnionWith(existingEndPoints);
                    prefixes.UnionWith(existingPrefixes);
                }

                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(endPoints.Count);

                        foreach (var endPoint in endPoints)
                        {
                            writer.Write(endPoint);
                        }

                        writer.Write(prefixes.Count);

                        foreach (var prefix in prefixes)
                        {
                            WritePrefix(writer, prefix);
                        }
                    }

                    payload = stream.ToArray();
                }

            }
            while (!await AddOrUpdateAsync());
        }

        private async Task WriteModulePrefixEntryAsync(ReadOnlyMemory<char> prefix, EndPointAddress endPoint, SessionIdentifier session, CancellationToken cancellation)
        {
            var normalizedPrefix = NormalizePrefix(prefix);

            if (normalizedPrefix.Span[0] == '_')
                throw new ArgumentException("A prefix must not begin with an underscore.");

            var path = GetPrefixPath(normalizedPrefix, endPoint, session, normalize: false);

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(endPoint);
                }

                var payload = stream.ToArray();
                var entry = await _coordinationManager.GetOrCreateAsync(path, payload, EntryCreationModes.Ephemeral, cancellation);
            }
        }

        

        private void WritePrefix(BinaryWriter writer, ReadOnlyMemory<char> prefix)
        {
            var normalizedPrefix = NormalizePrefix(prefix);

            using (ArrayPool<byte>.Shared.RentExact(Encoding.UTF8.GetByteCount(prefix.Span), out var memory))
            {
                var byteCount = Encoding.UTF8.GetBytes(prefix.Span, memory.Span);
                Assert(byteCount == memory.Length);

                writer.Write(byteCount);
                writer.Write(memory.Span);
            }
        }

        private static ReadOnlyMemory<char> NormalizePrefix(ReadOnlyMemory<char> prefix)
        {
            prefix = _whitespaceRegex.Replace(prefix.ToString(), "").AsMemory(); // TODO: This does take a copy

            if (prefix.Span.StartsWith("/".AsSpan()))
            {
                prefix = prefix.Slice(1);
            }

            return prefix;
        }

        private static CoordinationEntryPath GetPrefixPath(ReadOnlyMemory<char> prefix, bool normalize = true)
        {
            if (normalize)
            {
                prefix = NormalizePrefix(prefix);
            }

            return _rootPrefixesPath.GetChildPath(prefix);
        }

        private static CoordinationEntryPath GetPrefixPath(ReadOnlyMemory<char> prefix, EndPointAddress endPoint, SessionIdentifier session, bool normalize = true)
        {
            if (normalize)
            {
                prefix = NormalizePrefix(prefix);
            }

            var uniqueEntryName = IdGenerator.GenerateId(endPoint.ToString(), session.ToString());
            return _rootPrefixesPath.GetChildPath(prefix, uniqueEntryName.AsMemory());
        }

        private static CoordinationEntryPath GetRunningModulePath(ModuleIdentifier module)
        {
            return _rootRunningPath.GetChildPath(module.Name);
        }

        private static CoordinationEntryPath GetRunningModulePath(ModuleIdentifier module, SessionIdentifier session)
        {
            return _rootRunningPath.GetChildPath(module.Name, session.ToString());
        }
    }
}
