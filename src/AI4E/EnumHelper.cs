using System;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace AI4E
{
    public static partial class EnumHelper
    {
        private static partial class EnumType<TEnum>
            where TEnum : struct, Enum
        {
            public static bool IsFlagsEnum { get; }
            private static readonly Func<TEnum, bool> _isEnumValue;
            private static readonly Func<TEnum, bool> _isFlag;
            private static readonly Func<TEnum, TEnum, bool> _hasFlag;

            static EnumType()
            {
                IsFlagsEnum = IsFlagsEnumInternal();
                _isEnumValue = GetEnumValueEvaluator();
                _isFlag = GetFlagEvaluator();
                _hasFlag = GetHasFlagEvaluator();
            }

            public static bool IsEnumValue(TEnum value)
            {
                return _isEnumValue(value);
            }

            public static bool IsFlag(TEnum value)
            {
                return _isFlag(value);
            }

            public static bool HasFlag(TEnum value, TEnum flag)
            {
                return _hasFlag(value, flag);
            }

            private static bool IsFlagsEnumInternal()
            {
                var type = typeof(TEnum);

                Assert(type != null);
                Assert(type.IsEnum);

                return type.GetCustomAttribute<FlagsAttribute>() != null;
            }
        }

        public static bool IsFlagsEnum<TEnum>()
            where TEnum : struct, Enum
        {
            return EnumType<TEnum>.IsFlagsEnum;
        }

        public static bool IsFlag<TEnum>(this TEnum value)
            where TEnum : struct, Enum
        {
            return EnumType<TEnum>.IsFlag(value);
        }

        public static bool IsEnumValue<TEnum>(this TEnum value)
            where TEnum : struct, Enum
        {
            return EnumType<TEnum>.IsEnumValue(value);
        }

        public static bool IsValid<TEnum>(this TEnum value)
            where TEnum : struct, Enum
        {
            if (IsFlagsEnum<TEnum>())
            {
                return IsFlag(value);
            }

            return IsEnumValue(value);
        }

        public static bool IncludesFlag<TEnum>(this TEnum value, TEnum flag)
            where TEnum : struct, Enum
        {
            return EnumType<TEnum>.HasFlag(value, flag);
        }
    }
}
