using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Components.Build
{
    public static class Program
    {
        public static Task<int> Main(params string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option("--dll-file-path", "The file path to the Microsoft.AspNetCore.Components assembly")
                {
                    Required=true,
                    Argument = new Argument<string>()
                }
            };

            rootCommand.Handler = CommandHandler.Create<string>(async dllFilePath =>
            {
                var handler = new ReplaceComponentFactoryHandler
                {
                    DllFilePath = dllFilePath
                };
                var success = await handler.ExecuteAsync();

                return success ? 0 : -1;
            });

            return rootCommand.InvokeAsync(args);
        }
    }
}
