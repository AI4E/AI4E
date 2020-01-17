namespace AI4E.Utils.DependencyInjection.Autofac.Test.TestTypes
{
    public interface IOverridenTransientService { }

    public interface IOverridenTransientService<T> { }

    public sealed class OverridenTransientService : IOverridenTransientService { }

    public sealed class OverridenTransientService<T> : IOverridenTransientService<T> { }

    public sealed class OverridenTransientServiceOverride : IOverridenTransientService { }

    public sealed class OverridenTransientServiceOverride<T> : IOverridenTransientService<T> { }
}
