using System;
using System.Collections.Generic;
using AI4E.Coordination.Session;

namespace AI4E.Coordination
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
        CoordinationSession EphemeralOwner { get; }

        IEntry ToEntry();
    }
}
