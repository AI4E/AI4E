using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace AI4E
{
    public static class EnumHelper
    {
        private static ConcurrentDictionary<Type, bool> _isFlagsEnumLookup = new ConcurrentDictionary<Type, bool>();
        private static ConcurrentDictionary<Type, object> _isFlagLookup = new ConcurrentDictionary<Type, object>();
        private static ConcurrentDictionary<Type, object> _isEnumValueLookup = new ConcurrentDictionary<Type, object>();

        public static bool IsFlagsEnum<TEnum>()
            where TEnum : Enum
        {
            var type = typeof(TEnum);

            return _isFlagsEnumLookup.GetOrAdd(type, IsFlagsEnumInternal);
        }

        public static bool IsFlag<TEnum>(this TEnum value)
            where TEnum : Enum
        {
            var enumType = typeof(TEnum);
            var evaluator = _isFlagLookup.GetOrAdd(enumType, _ => GetFlagEvaluator<TEnum>()) as Func<TEnum, bool>;
            Assert(evaluator != null);
            return evaluator(value);
        }

        public static bool IsEnumValue<TEnum>(this TEnum value)
            where TEnum : Enum
        {
            var enumType = typeof(TEnum);
            var evaluator = _isEnumValueLookup.GetOrAdd(enumType, _ => GetEnumValueEvaluator<TEnum>()) as Func<TEnum, bool>;
            Assert(evaluator != null);
            return evaluator(value);
        }

        public static bool IsValid<TEnum>(this TEnum value)
            where TEnum : Enum
        {
            if (IsFlagsEnum<TEnum>())
            {
                return IsFlag(value);
            }
            return IsEnumValue(value);
        }

        private static bool IsFlagsEnumInternal(Type type)
        {
            Assert(type != null);
            Assert(type.IsEnum);

            return type.GetCustomAttribute<FlagsAttribute>() != null;
        }

        private static Func<TEnum, bool> GetFlagEvaluator<TEnum>()
            where TEnum : Enum
        {
            var enumType = typeof(TEnum);
            Assert(enumType != null);
            Assert(enumType.IsEnum);

            var underlyingType = Enum.GetUnderlyingType(enumType);

            if (underlyingType == typeof(byte))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (byte)(object)p).Aggregate(seed: (byte)0, (e, c) => (byte)(e | c));

                return value => (((byte)(object)value) & ~comparand) == 0;

            }
            else if (underlyingType == typeof(sbyte))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (sbyte)(object)p).Aggregate(seed: (sbyte)0, (e, c) => (sbyte)(e | c));

                return value => (((sbyte)(object)value) & ~comparand) == 0;
            }
            else if (underlyingType == typeof(ushort))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (ushort)(object)p).Aggregate(seed: (ushort)0, (e, c) => (ushort)(e | c));

                return value => (((ushort)(object)value) & ~comparand) == 0;
            }
            else if (underlyingType == typeof(short))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (short)(object)p).Aggregate(seed: (short)0, (e, c) => (short)(e | c));

                return value => (((short)(object)value) & ~comparand) == 0;
            }
            else if (underlyingType == typeof(uint))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (uint)(object)p).Aggregate(seed: (uint)0, (e, c) => (uint)(e | c));

                return value => (((uint)(object)value) & ~comparand) == 0;
            }
            else if (underlyingType == typeof(int))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (int)(object)p).Aggregate(seed: (int)0, (e, c) => (int)(e | c));

                return value => (((int)(object)value) & ~comparand) == 0;
            }
            else if (underlyingType == typeof(ulong))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (ulong)(object)p).Aggregate(seed: (ulong)0, (e, c) => (ulong)(e | c));

                return value => (((ulong)(object)value) & ~comparand) == 0;
            }
            else if (underlyingType == typeof(long))
            {
                var values = (TEnum[])Enum.GetValues(enumType);
                var comparand = values.Select(p => (long)(object)p).Aggregate(seed: (long)0, (e, c) => (long)(e | c));

                return value => (((long)(object)value) & ~comparand) == 0;
            }
            else
            {
                throw new InvalidOperationException("Only enums with primitive numeric underlying types are supported.");
            }
        }

        private static Func<TEnum, bool> GetEnumValueEvaluator<TEnum>()
            where TEnum : Enum
        {
            var enumType = typeof(TEnum);
            Assert(enumType != null);
            Assert(enumType.IsEnum);

            var underlyingType = Enum.GetUnderlyingType(enumType);

            if (underlyingType == typeof(byte))
            {
                var values = new HashSet<byte>(((TEnum[])Enum.GetValues(enumType)).Select(p => (byte)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (byte)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (byte)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((byte)(object)value);
                    }
                }

                return value =>
                {
                    var v = (byte)(object)value;
                    return v >= min && v <= max;
                };
            }

            if (underlyingType == typeof(sbyte))
            {
                var values = new HashSet<sbyte>(((TEnum[])Enum.GetValues(enumType)).Select(p => (sbyte)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (sbyte)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (sbyte)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((sbyte)(object)value);
                    }
                }

                return value =>
                {
                    var v = (sbyte)(object)value;
                    return v >= min && v <= max;
                };
            }

            if (underlyingType == typeof(ushort))
            {
                var values = new HashSet<ushort>(((TEnum[])Enum.GetValues(enumType)).Select(p => (ushort)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (ushort)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (ushort)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((ushort)(object)value);
                    }
                }

                return value =>
                {
                    var v = (ushort)(object)value;
                    return v >= min && v <= max;
                };
            }

            if (underlyingType == typeof(short))
            {
                var values = new HashSet<short>(((TEnum[])Enum.GetValues(enumType)).Select(p => (short)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (short)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (short)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((short)(object)value);
                    }
                }

                return value =>
                {
                    var v = (short)(object)value;
                    return v >= min && v <= max;
                };
            }

            if (underlyingType == typeof(uint))
            {
                var values = new HashSet<uint>(((TEnum[])Enum.GetValues(enumType)).Select(p => (uint)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (uint)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (uint)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((uint)(object)value);
                    }
                }

                return value =>
                {
                    var v = (uint)(object)value;
                    return v >= min && v <= max;
                };
            }

            if (underlyingType == typeof(int))
            {
                var values = new HashSet<int>(((TEnum[])Enum.GetValues(enumType)).Select(p => (int)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (int)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (int)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((int)(object)value);
                    }
                }

                return value =>
                {
                    var v = (int)(object)value;
                    return v >= min && v <= max;
                };
            }

            if (underlyingType == typeof(ulong))
            {
                var values = new HashSet<ulong>(((TEnum[])Enum.GetValues(enumType)).Select(p => (ulong)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (ulong)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (ulong)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((ulong)(object)value);
                    }
                }

                return value =>
                {
                    var v = (ulong)(object)value;
                    return v >= min && v <= max;
                };
            }

            if (underlyingType == typeof(long))
            {
                var values = new HashSet<long>(((TEnum[])Enum.GetValues(enumType)).Select(p => (long)(object)p));

                if (values.Count == 0)
                {
                    return _ => false;
                }

                if (values.Count == 1)
                {
                    return value => values.First() == (long)(object)value;
                }

                var min = values.Min();
                var max = values.Max();

                Assert(min < max);

                for (var i = (long)(min + 1); i < max; i++)
                {
                    if (!values.Contains(i))
                    {
                        return value => values.Contains((long)(object)value);
                    }
                }

                return value =>
                {
                    var v = (long)(object)value;
                    return v >= min && v <= max;
                };
            }

            throw new InvalidOperationException("Only enums with primitive numeric underlying types are supported.");

        }
    }
}
