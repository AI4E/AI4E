using System;
using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class FileSystemModuleSourceLocationChanged : DomainEvent
    {
        public FileSystemModuleSourceLocationChanged(Guid aggregateId, FileSystemModuleSourceLocation location) : base(aggregateId)
        {
            if (location == default)
                throw new ArgumentDefaultException(nameof(location));

            Location = location;
        }

        public FileSystemModuleSourceLocation Location { get; }
    }
}
