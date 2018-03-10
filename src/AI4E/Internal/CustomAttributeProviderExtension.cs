using System;
using System.Reflection;

namespace AI4E.Internal
{
    internal static class CustomAttributeProviderExtension
    {
        public static bool IsDefined<TCustomAttribute>(this ICustomAttributeProvider attributeProvider) 
            where TCustomAttribute : Attribute
        {
            if (attributeProvider == null)
                throw new ArgumentNullException(nameof(attributeProvider));

            return attributeProvider.IsDefined(typeof(TCustomAttribute), inherit: false);
        }

        public static bool IsDefined<TCustomAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit) 
            where TCustomAttribute : Attribute
        {
            if (attributeProvider == null)
                throw new ArgumentNullException(nameof(attributeProvider));

            return attributeProvider.IsDefined(typeof(TCustomAttribute), inherit);
        }
    }
}
