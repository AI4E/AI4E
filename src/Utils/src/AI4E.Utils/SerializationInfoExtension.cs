using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.Serialization
{
    public static class AI4EUtilsSerializationInfoExtension
    {
        public static void AddValue<TValue>(
            this SerializationInfo serializationInfo, string name, [MaybeNull] TValue value)
        {
#pragma warning disable CA1062
            serializationInfo.AddValue(name, value, typeof(TValue));
#pragma warning restore CA1062
        }

        public static bool TryGetValue<TValue>(
            this SerializationInfo serializationInfo, string name, [MaybeNullWhen(false)] out TValue value)
        {
#pragma warning disable CA1062
            var result = serializationInfo.GetValue(name, typeof(TValue));
#pragma warning restore CA1062

            if (result is null)
            {
                value = default!;
                return false;
            }

            value = (TValue)result;
            return true;
        }

        [return: MaybeNull, NotNullIfNotNull("defaultValue")]
        public static TValue GetValueOrDefault<TValue>(
                    this SerializationInfo serializationInfo, string name, [MaybeNull] TValue defaultValue = default)
        {
            if (!serializationInfo.TryGetValue<TValue>(name, out var result))
            {
                result = defaultValue;
            }

            return result;
        }
    }
}
