namespace AI4E.Utils.DependencyInjection.Autofac.Test.TestTypes
{
    public interface IOverridenSingletonService { }

    public interface IOverridenSingletonService<T> { }

    public sealed class OverridenSingletonService : IOverridenSingletonService { }

    public sealed class OverridenSingletonService<T> : IOverridenSingletonService<T> { }

    public sealed class OverridenSingletonServiceOverride : IOverridenSingletonService { }

    public sealed class OverridenSingletonServiceOverride<T> : IOverridenSingletonService<T> { }
}
