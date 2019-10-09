using System;

namespace AI4E.Coordination
{
    /// <summary>
    /// Represents a provider for the specified type.
    /// </summary>
    /// <typeparam name="T">The type that the provider can deliver an instance of.</typeparam>
    [Obsolete]
    public interface IProvider<out T>
    {
        /// <summary>
        /// Provides an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <returns>An object of type <typeparamref name="T"/>.</returns>
        T ProvideInstance();
    }
}
