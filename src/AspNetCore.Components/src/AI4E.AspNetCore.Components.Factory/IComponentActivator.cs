using System;
using Microsoft.AspNetCore.Components;

namespace AI4E.AspNetCore.Components.Factory
{
    public interface IComponentActivator
    {
        IComponent ActivateComponent(Type componentType);
    }
}
