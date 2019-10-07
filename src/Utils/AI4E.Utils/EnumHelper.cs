/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Diagnostics.Debug;

namespace AI4E.Utils
{
    public static partial class EnumHelper
    {
        private static partial class EnumType<TEnum>
            where TEnum : struct, Enum
        {
            public static bool IsFlagsEnum { get; } = IsFlagsEnumInternal();
            private static readonly Func<TEnum, bool> _isEnumValue= GetEnumValueEvaluator();
            private static readonly Func<TEnum, bool> _isFlag= GetFlagEvaluator();
            private static readonly Func<TEnum, TEnum, bool> _hasFlag = GetHasFlagEvaluator();

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
                Assert(type!.IsEnum);

                return type.GetCustomAttribute<FlagsAttribute>() != null;
            }

            private static Func<TEnum, bool> GetFlagEvaluator()
            {
                var enumType = typeof(TEnum);
                Assert(enumType != null);
                Assert(enumType!.IsEnum);

                var underlyingType = Enum.GetUnderlyingType(enumType);

                if (underlyingType == typeof(byte))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, byte>(ref p)).Aggregate(seed: (byte)0, (e, c) => (byte)(e | c));

                    return value => ((Unsafe.As<TEnum, byte>(ref value)) & ~comparand) == 0;
                }
                else if (underlyingType == typeof(sbyte))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, sbyte>(ref p)).Aggregate(seed: (sbyte)0, (e, c) => (sbyte)(e | c));

                    return value => ((Unsafe.As<TEnum, sbyte>(ref value)) & ~comparand) == 0;
                }
                else if (underlyingType == typeof(ushort))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, ushort>(ref p)).Aggregate(seed: (ushort)0, (e, c) => (ushort)(e | c));

                    return value => ((Unsafe.As<TEnum, ushort>(ref value)) & ~comparand) == 0;
                }
                else if (underlyingType == typeof(short))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, short>(ref p)).Aggregate(seed: (short)0, (e, c) => (short)(e | c));

                    return value => ((Unsafe.As<TEnum, short>(ref value)) & ~comparand) == 0;
                }
                else if (underlyingType == typeof(uint))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, uint>(ref p)).Aggregate(seed: (uint)0, (e, c) => (uint)(e | c));

                    return value => ((Unsafe.As<TEnum, uint>(ref value)) & ~comparand) == 0;
                }
                else if (underlyingType == typeof(int))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, int>(ref p)).Aggregate(seed: (int)0, (e, c) => (int)(e | c));

                    return value => ((Unsafe.As<TEnum, int>(ref value)) & ~comparand) == 0;
                }
                else if (underlyingType == typeof(ulong))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, ulong>(ref p)).Aggregate(seed: (ulong)0, (e, c) => (ulong)(e | c));

                    return value => ((Unsafe.As<TEnum, ulong>(ref value)) & ~comparand) == 0;
                }
                else if (underlyingType == typeof(long))
                {
                    var values = (TEnum[])Enum.GetValues(enumType);
                    var comparand = values.Select(p => Unsafe.As<TEnum, long>(ref p)).Aggregate(seed: (long)0, (e, c) => (long)(e | c));

                    return value => ((Unsafe.As<TEnum, long>(ref value)) & ~comparand) == 0;
                }
                else
                {
                    throw new InvalidOperationException("Only enums with primitive numeric underlying types are supported.");
                }
            }

            private static Func<TEnum, TEnum, bool> GetHasFlagEvaluator()
            {
                var enumType = typeof(TEnum);
                Assert(enumType != null);
                Assert(enumType!.IsEnum);

                var underlyingType = Enum.GetUnderlyingType(enumType);

                if (underlyingType == typeof(byte))
                {
                    return (value, flag) => (Unsafe.As<TEnum, byte>(ref value) & Unsafe.As<TEnum, byte>(ref flag)) != 0;
                }

                if (underlyingType == typeof(sbyte))
                {
                    return (value, flag) => (Unsafe.As<TEnum, sbyte>(ref value) & Unsafe.As<TEnum, sbyte>(ref flag)) != 0;
                }

                if (underlyingType == typeof(ushort))
                {
                    return (value, flag) => (Unsafe.As<TEnum, ushort>(ref value) & Unsafe.As<TEnum, ushort>(ref flag)) != 0;
                }

                if (underlyingType == typeof(short))
                {
                    return (value, flag) => (Unsafe.As<TEnum, short>(ref value) & Unsafe.As<TEnum, short>(ref flag)) != 0;
                }

                if (underlyingType == typeof(uint))
                {
                    return (value, flag) => (Unsafe.As<TEnum, uint>(ref value) & Unsafe.As<TEnum, uint>(ref flag)) != 0;
                }

                if (underlyingType == typeof(int))
                {
                    return (value, flag) => (Unsafe.As<TEnum, int>(ref value) & Unsafe.As<TEnum, int>(ref flag)) != 0;
                }

                if (underlyingType == typeof(ulong))
                {
                    return (value, flag) => (Unsafe.As<TEnum, ulong>(ref value) & Unsafe.As<TEnum, ulong>(ref flag)) != 0;
                }

                if (underlyingType == typeof(long))
                {
                    return (value, flag) => (Unsafe.As<TEnum, long>(ref value) & Unsafe.As<TEnum, long>(ref flag)) != 0;
                }

                throw new InvalidOperationException("Only enums with primitive numeric underlying types are supported.");
            }

            private static Func<TEnum, bool> GetEnumValueEvaluator()
            {
                var enumType = typeof(TEnum);
                Assert(enumType != null);
                Assert(enumType!.IsEnum);

                var underlyingType = Enum.GetUnderlyingType(enumType);

                if (underlyingType == typeof(byte))
                {
                    var values = new HashSet<byte>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, byte>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, byte>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (byte)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, byte>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, byte>(ref value);
                        return v >= min && v <= max;
                    };
                }

                if (underlyingType == typeof(sbyte))
                {
                    var values = new HashSet<sbyte>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, sbyte>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, sbyte>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (sbyte)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, sbyte>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, sbyte>(ref value);
                        return v >= min && v <= max;
                    };
                }

                if (underlyingType == typeof(ushort))
                {
                    var values = new HashSet<ushort>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, ushort>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, ushort>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (ushort)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, ushort>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, ushort>(ref value);
                        return v >= min && v <= max;
                    };
                }

                if (underlyingType == typeof(short))
                {
                    var values = new HashSet<short>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, short>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, short>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (short)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, short>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, short>(ref value);
                        return v >= min && v <= max;
                    };
                }

                if (underlyingType == typeof(uint))
                {
                    var values = new HashSet<uint>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, uint>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, uint>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (uint)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, uint>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, uint>(ref value);
                        return v >= min && v <= max;
                    };
                }

                if (underlyingType == typeof(int))
                {
                    var values = new HashSet<int>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, int>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, int>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (int)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, int>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, int>(ref value);
                        return v >= min && v <= max;
                    };
                }

                if (underlyingType == typeof(ulong))
                {
                    var values = new HashSet<ulong>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, ulong>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, ulong>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (ulong)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, ulong>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, ulong>(ref value);
                        return v >= min && v <= max;
                    };
                }

                if (underlyingType == typeof(long))
                {
                    var values = new HashSet<long>(((TEnum[])Enum.GetValues(enumType)).Select(p => Unsafe.As<TEnum, long>(ref p)));

                    if (values.Count == 0)
                    {
                        return _ => false;
                    }

                    if (values.Count == 1)
                    {
                        // We evaluate this here, in order that the hash-set is not captured by the lambda but only the single value.
                        var singleValue = values.First();
                        return value => singleValue == Unsafe.As<TEnum, long>(ref value);
                    }

                    var min = values.Min();
                    var max = values.Max();

                    Assert(min < max);

                    for (var i = (long)(min + 1); i < max; i++)
                    {
                        if (!values.Contains(i))
                        {
                            return value => values.Contains(Unsafe.As<TEnum, long>(ref value));
                        }
                    }

                    return value =>
                    {
                        var v = Unsafe.As<TEnum, long>(ref value);
                        return v >= min && v <= max;
                    };
                }

                throw new InvalidOperationException("Only enums with primitive numeric underlying types are supported.");
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
