using System;
using Microsoft.AspNetCore.Blazor;
using Microsoft.AspNetCore.Blazor.RenderTree;

namespace AI4E.Blazor.Components
{
    public sealed class ViewExtensionRenderer
    {
        public RenderFragment RenderViewExtension<TViewExtension>(Action<TViewExtension> config)
        {
            void Result(RenderTreeBuilder builder)
            {
                builder.OpenComponent(0, typeof(ViewExtension<>).MakeGenericType(typeof(TViewExtension)));
                builder.AddAttribute(0, ViewExtension<object>.ConfigurationName, config);
                builder.CloseComponent();
            }

            return Result;
        }
        public RenderFragment RenderViewExtension<TViewExtension>()
        {
            void Result(RenderTreeBuilder builder)
            {
                builder.OpenComponent(0, typeof(ViewExtension<>).MakeGenericType(typeof(TViewExtension)));
                builder.CloseComponent();
            }

            return Result;
        }
    }
}
