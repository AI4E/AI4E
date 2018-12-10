using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;

namespace AI4E.Modularity.ModulePacker
{
    // For the packer to work, there must be specified the following arguments:
    // The input assembly path (path to the entry assembly of the module.) The packer assumes that all dependencies are located in the same or a subdirectory.
    // The output assembly, where the package shall be put.
    internal static class Program
    {
        private static readonly IMetadataReader _metadataReader = new MetadataReader();
        private static readonly IMetadataWriter _metadataWriter = new MetadataWriter();
        private static readonly IMetadataAccessor _metadataAccessor = new MetadataAccessor(_metadataReader);

        private static void Main(string[] args)
        {
            // TODO: Validate arguments

            var inputAssemblyPath = args[0];
            var outputDir = args[1];

            using (var cancellationSource = new CancellationTokenSource())
            {
                Task.Run(() => RunWithExit(inputAssemblyPath, outputDir, cancellationSource.Token));

                Console.CancelKeyPress += (s, e) => cancellationSource.Cancel();

                Thread.Sleep(Timeout.Infinite);
            }
        }

        private static async void RunWithExit(string inputAssemblyPath, string outputDir, CancellationToken cancellation)
        {
            try
            {
                await RunAsync(inputAssemblyPath, outputDir, cancellation);
                Environment.Exit(0);
            }
            catch (Exception exc)
            {
                await Console.Out.WriteLineAsync(exc.Message);
                Environment.Exit(1);
            }
        }

        private static async Task RunAsync(string inputAssemblyPath, string outputDir, CancellationToken cancellation)
        {
            var metadata = await GetMetadataAsync(inputAssemblyPath, cancellation);
            var inputDir = Path.GetDirectoryName(inputAssemblyPath);
            var outputFilePath = Path.Combine(outputDir, metadata.Release.ToString() + ".aep");

            using (var stream = new MemoryStream())
            {
                await WritePackageToStreamAsync(metadata, inputDir, stream, cancellation);
                await WriteToFileAsync(stream, outputFilePath, cancellation);
            }
        }

        private static async Task<IModuleMetadata> GetMetadataAsync(string inputAssemblyPath, CancellationToken cancellation)
        {
            // We take our core assembly for now. 
            var coreAssembly = typeof(object).Assembly; // TODO: Support specifying a different core assembly to support additional workloads.
            var assemblyResolver = new PathAssemblyResolver(new string[] { inputAssemblyPath, coreAssembly.Location });

            using (var loadContext = new MetadataLoadContext(assemblyResolver, coreAssembly.FullName))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(inputAssemblyPath);
                var assembly = loadContext.LoadFromAssemblyName(assemblyName);

                var metadata = await _metadataAccessor.GetMetadataAsync(assembly, cancellation);

                if (metadata.ReleaseDate == default)
                {
                    var editableMetadata = metadata as ModuleMetadata ?? new ModuleMetadata(metadata);
                    editableMetadata.ReleaseDate = DateTime.Now;
                    metadata = editableMetadata;
                }

                return metadata;
            }
        }

        private static async Task WritePackageToStreamAsync(IModuleMetadata metadata, string inputDir, MemoryStream stream, CancellationToken cancellation)
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                await PackDirectoryAsync(archive, inputDir, cancellation);

                var manifestEntry = archive.CreateEntry("module.json");
                using (var manifestEntryStream = manifestEntry.Open())
                {
                    await _metadataWriter.WriteMetadataAsync(manifestEntryStream, metadata, cancellation);
                }
            }
        }

        private static async Task PackDirectoryAsync(ZipArchive archive, string inputDir, CancellationToken cancellation)
        {
            var fullPath = Path.GetFullPath(inputDir);

            foreach (var file in Directory.EnumerateFiles(inputDir, searchPattern: "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file).Equals("module.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullFilePath = Path.GetFullPath(file);
                var entryName = fullFilePath.Substring(fullPath.Length).TrimStart('/', '\\');
                await archive.AddFileAsEntryAsync(entryName, fullFilePath, cancellation);
            }
        }

        private static async Task WriteToFileAsync(MemoryStream stream, string outputFilePath, CancellationToken cancellation)
        {
            var outputDir = Path.GetDirectoryName(outputFilePath);

            while (true)
            {
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                try
                {
                    stream.Position = 0;

                    using (var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 4096, useAsync: true))
                    {
                        await stream.CopyToAsync(fileStream, cancellation);
                    }

                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }
            }
        }
    }
}
