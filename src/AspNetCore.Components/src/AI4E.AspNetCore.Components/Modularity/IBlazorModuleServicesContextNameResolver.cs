namespace AI4E.AspNetCore.Components.Modularity
{
    public interface IBlazorModuleServicesContextNameResolver
    {
        string ResolveServicesContextName(IBlazorModuleDescriptor moduleDescriptor);
    }
}