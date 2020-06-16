using System;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore.Components.Modularity
{
    public interface IBlazorModuleSourceFactory
    {
        IBlazorModuleSource CreateModuleSource();
    }
}
