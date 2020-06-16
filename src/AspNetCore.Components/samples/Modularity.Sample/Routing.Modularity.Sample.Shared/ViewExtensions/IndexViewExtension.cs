using AI4E.AspNetCore.Components.Extensibility;
using Microsoft.AspNetCore.Components;

namespace Routing.Modularity.Sample.ViewExtensions
{
    public class IndexViewExtension : ViewExtensionBase
    {
        [Parameter] public string Message { get; set; }
        [Parameter] public int Number { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
    }
}
