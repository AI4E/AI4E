using AI4E.AspNetCore.Components.Extensibility;
using Microsoft.AspNetCore.Components;

namespace Routing.ModularRouterSample.ViewExtensionDefinitions
{
    public class TypedViewExtensionDefinition : ViewExtensionBase
    {
        [Parameter] public string Message { get; set; }
        [Parameter] public int Number { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
    }
}
