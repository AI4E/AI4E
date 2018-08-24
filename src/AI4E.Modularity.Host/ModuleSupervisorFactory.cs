using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleSupervisorFactory : IModuleSupervisorFactory
    {
        private readonly IMetadataReader _metadataReader;
        private readonly ILoggerFactory _loggerFactory;

        public ModuleSupervisorFactory(IMetadataReader metadataReader, ILoggerFactory loggerFactory = null)
        {
            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            _metadataReader = metadataReader;
            _loggerFactory = loggerFactory;
        }

        public IModuleSupervisor CreateSupervisor(DirectoryInfo directory)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            var logger = _loggerFactory?.CreateLogger<ModuleSupervisor>();

            return new ModuleSupervisor(directory, _metadataReader, logger);
        }
    }
}
