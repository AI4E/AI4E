using AI4E.AspNetCore.Components.Extensibility;

namespace Routing.ModularRouterSample.ViewExtensionDefinitions
{
    public interface IIndexPageViewExtensionDefinition : IViewExtensionDefinition<IndexPageViewExtensionContext>
    { }

    public sealed class IndexPageViewExtensionContext
    {
        public string Message { get; set; }
        public int Number { get; set; }
    }
}
