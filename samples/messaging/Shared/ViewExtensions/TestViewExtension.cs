namespace Shared.ViewExtensions
{
    public sealed class TestViewExtension
    {
        public TestViewExtension(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }
}
