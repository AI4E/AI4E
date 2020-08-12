using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AI4E.Storage.MongoDB.Test
{
    internal sealed class InputJsonLoader
    {
        public static InputJsonLoader Instance { get; } = new InputJsonLoader();

        private InputJsonLoader() { }

        public string LoadJsonInput(
           [CallerMemberName] string testMethodName = "")
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = GetType().Namespace + "." + testMethodName + "Input.json";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
