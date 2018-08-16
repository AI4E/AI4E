using System;

namespace AI4E
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class InjectAttribute : Attribute
    {
        public InjectAttribute() { }
        public InjectAttribute(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            ServiceType = serviceType;
        }

        public Type ServiceType { get; }
    }
}
