namespace AI4E.Utils.DependencyInjection.Autofac.Test.TestTypes
{
    public interface IOverridenScopedService { }

    public interface IOverridenScopedService<T> { }

    public sealed class OverridenScopedService : IOverridenScopedService { }

    public sealed class OverridenScopedService<T> : IOverridenScopedService<T> { }

    public sealed class OverridenScopedServiceOverride : IOverridenScopedService { }

    public sealed class OverridenScopedServiceOverride<T> : IOverridenScopedService<T> { }
}
