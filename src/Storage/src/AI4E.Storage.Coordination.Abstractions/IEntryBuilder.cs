using System;
using System.Collections.Generic;
using AI4E.Storage.Coordination.Session;

namespace AI4E.Storage.Coordination
{
    public interface IEntryBuilder
    {
        IList<CoordinationEntryPathSegment> Children { get; }
        ICoordinationManager CoordinationManager { get; }
        DateTime CreationTime { get; }
        DateTime LastWriteTime { get; }
        CoordinationEntryPathSegment Name { get; }
        CoordinationEntryPath ParentPath { get; }
        CoordinationEntryPath Path { get; }
        ReadOnlyMemory<byte> Value { get; set; }
        int Version { get; }
        SessionIdentifier EphemeralOwner { get; }

        IEntry ToEntry();
    }
}
