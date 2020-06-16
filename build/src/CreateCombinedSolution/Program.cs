using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateCombinedSolution
{
    public static class Program
    {
        private const string _solutionFileExtension = ".sln";
        private const string _projectFileExtension = ".csproj";
        private const string _projectSearchPattern = "*" + _projectFileExtension;
        private const string _srcDirectoryName = "src";
        private const string _testDirectoryName = "test";
        private const string _samplesDirectoryName = "samples";

        public static async Task Main(string sourcePath, string solutionName, string? solutionDir = null, string[]? dependencyPaths = null)
        {
            if (sourcePath is null)
                throw new ArgumentNullException(nameof(sourcePath));

            sourcePath = Path.GetFullPath(sourcePath);

            if (solutionName is null)
                throw new ArgumentNullException(nameof(solutionName));

            Console.WriteLine($"Combining projects in solution {solutionName}...");

            solutionDir ??= Directory.GetCurrentDirectory();
            solutionDir = Path.GetFullPath(solutionDir);

            if (solutionName.EndsWith(_solutionFileExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                solutionName = solutionName[..^4];
            }

            var solutionPath = Path.Combine(solutionDir, solutionName + _solutionFileExtension);

            if (File.Exists(solutionPath))
            {
                File.Delete(solutionPath);
            }

            if (!Directory.Exists(solutionDir))
            {
                Directory.CreateDirectory(solutionDir);
            }

            var projects = EnumerateProjects(sourcePath);
            var dependencyProjects = EnumerateDependencyProjects(dependencyPaths?.Select(Path.GetFullPath).ToArray() ?? new string[0]);

            Console.WriteLine($"Creating solution {solutionName}.");
            await CreateSolutionAsync(solutionDir, solutionName);

            foreach (var project in projects)
            {
                var solutionFolder = _srcDirectoryName;

                if (project.Contains(_testDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    solutionFolder = _testDirectoryName;
                }
                else if (project.Contains(_samplesDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Console.WriteLine($"Add project '{Path.GetFileName(project)[..^_projectFileExtension.Length]}' in solution folder '{solutionFolder}'.");

                await AddProjectToSolution(solutionDir, solutionName, project, solutionFolder);
            }

            foreach (var project in dependencyProjects)
            {
                if (project.Contains(_testDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                else if (project.Contains(_samplesDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Console.WriteLine($"Add dependency project '{Path.GetFileName(project)[..^_projectFileExtension.Length]}' in solution folder 'dependencies'.");

                await AddProjectToSolution(solutionDir, solutionName, project, "dependencies");
            }

            Console.WriteLine("Successfully combined projects.");
        }

        private static readonly StringBuilder _errorBuilder = new StringBuilder();

        private static async Task AddProjectToSolution(
            string solutionDir,
            string solutionName,
            string projectPath,
            string solutionFolder)
        {
            _errorBuilder.Clear();

            var result = await Process.ExecuteAsync(
                "dotnet",
                $"sln {solutionName}.sln add --solution-folder {solutionFolder} {projectPath}",
                workingDir: solutionDir,
                stdErr: error => _errorBuilder.Append(error));

            if (result != 0)
            {
                PrintError("Unable to add project to solution.");
                PrintError(_errorBuilder.ToString());
                Environment.Exit(-1);
            }
        }

        private static async Task CreateSolutionAsync(string solutionDir, string solutionName)
        {
            _errorBuilder.Clear();

            var result = await Process.ExecuteAsync(
                "dotnet",
                $"new sln --name {solutionName}",
                workingDir: solutionDir,
                stdErr: error => _errorBuilder.Append(error));

            if (result != 0)
            {
                PrintError("Unable to create solution.");
                PrintError(_errorBuilder.ToString());
                Environment.Exit(-1);
            }
        }

        private static IEnumerable<string> EnumerateDependencyProjects(string[] dependencyPaths)
        {
            return dependencyPaths.SelectMany(EnumerateProjects);
        }

        private static IEnumerable<string> EnumerateProjects(string path)
        {
            return Directory.EnumerateFiles(path, _projectSearchPattern, SearchOption.AllDirectories);
        }

        private static void PrintError(string error)
        {
            var color = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Red;

                Console.WriteLine(error);
            }
            finally
            {
                Console.ForegroundColor = color;
            }
        }
    }
}
