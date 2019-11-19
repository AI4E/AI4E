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
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace AI4E.Utils.Memory.Compatibility
{
    public static class GuidHelper
    {
        private static readonly CreateGuidShim? _createGuidShim = BuildCreateGuidShim(typeof(Guid));
        private static readonly ParseShim? _parseShim = BuildParseShim(typeof(Guid));
        private static readonly ParseExectShim? _parseExectShim = BuildParseExactShim(typeof(Guid));
        private static readonly TryParseShim? _tryParseShim = BuildTryParseShim(typeof(Guid));
        private static readonly TryParseExactShim? _tryParseExactShim = BuildTryParseExactShim(typeof(Guid));

        private static CreateGuidShim? BuildCreateGuidShim(Type guidType)
        {
            var ctor = guidType.GetConstructor(BindingFlags.Public | BindingFlags.Instance,
                                               Type.DefaultBinder,
                                               new Type[] { typeof(ReadOnlySpan<byte>) },
                                               modifiers: null);

            if (ctor == null)
            {
                return null;
            }

            var bParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "b");
            var call = Expression.New(ctor, bParameter);
            var lambda = Expression.Lambda<CreateGuidShim>(call, bParameter);
            return lambda.Compile();
        }

        private static ParseShim? BuildParseShim(Type guidType)
        {
            var parseMethod = guidType.GetMethod(nameof(Guid.Parse),
                                                 BindingFlags.Public | BindingFlags.Static,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>) },
                                                 modifiers: null);

            if (parseMethod == null)
            {
                return null;
            }

            Assert(parseMethod.ReturnType == typeof(Guid));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var call = Expression.Call(parseMethod, inputParameter);
            var lambda = Expression.Lambda<ParseShim>(call, inputParameter);
            return lambda.Compile();
        }

        private static ParseExectShim? BuildParseExactShim(Type guidType)
        {
            var parseExactMethod = guidType.GetMethod(nameof(Guid.ParseExact),
                                                 BindingFlags.Public | BindingFlags.Static,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>) },
                                                 modifiers: null);

            if (parseExactMethod == null)
            {
                return null;
            }

            Assert(parseExactMethod.ReturnType == typeof(Guid));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var formatParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "format");
            var call = Expression.Call(parseExactMethod, inputParameter, formatParameter);
            var lambda = Expression.Lambda<ParseExectShim>(call, inputParameter, formatParameter);
            return lambda.Compile();
        }

        private static TryParseShim? BuildTryParseShim(Type guidType)
        {
            var tryParseMethod = guidType.GetMethod(nameof(Guid.TryParse),
                                                 BindingFlags.Public | BindingFlags.Static,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>), typeof(Guid).MakeByRefType() },
                                                 modifiers: null);

            if (tryParseMethod == null)
            {
                return null;
            }

            Assert(tryParseMethod.ReturnType == typeof(bool));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var resultParameter = Expression.Parameter(typeof(Guid).MakeByRefType(), "result");
            var call = Expression.Call(tryParseMethod, inputParameter, resultParameter);
            var lambda = Expression.Lambda<TryParseShim>(call, inputParameter, resultParameter);
            return lambda.Compile();
        }

        private static TryParseExactShim? BuildTryParseExactShim(Type guidType)
        {
            var tryParseExactMethod = guidType.GetMethod(nameof(Guid.TryParseExact),
                                                  BindingFlags.Public | BindingFlags.Static,
                                                  Type.DefaultBinder,
                                                  new Type[] { typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(Guid).MakeByRefType() },
                                                  modifiers: null);

            if (tryParseExactMethod == null)
            {
                return null;
            }

            Assert(tryParseExactMethod.ReturnType == typeof(bool));

            var inputParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var formatParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "format");
            var resultParameter = Expression.Parameter(typeof(Guid).MakeByRefType(), "result");
            var call = Expression.Call(tryParseExactMethod, inputParameter, formatParameter, resultParameter);
            var lambda = Expression.Lambda<TryParseExactShim>(call, inputParameter, formatParameter, resultParameter);
            return lambda.Compile();
        }

        private delegate Guid CreateGuidShim(ReadOnlySpan<byte> b);
        private delegate Guid ParseShim(ReadOnlySpan<char> input);
        private delegate Guid ParseExectShim(ReadOnlySpan<char> input, ReadOnlySpan<char> format);
        private delegate bool TryParseShim(ReadOnlySpan<char> input, out Guid result);
        private delegate bool TryParseExactShim(ReadOnlySpan<char> input, ReadOnlySpan<char> format, out Guid result);

        public static Guid CreateGuid(ReadOnlySpan<byte> b)
        {
            if (_createGuidShim != null)
            {
                return _createGuidShim(b);
            }

            if ((uint)b.Length != 16)
            {
                throw new ArgumentException("The span must be 16 bytes long.", nameof(b));
            }

            // Adapted from: https://github.com/dotnet/corefx/blob/b51c5b8bed06f924b5470d9042d4de0381dd89c9/src/Common/src/CoreLib/System/Guid.cs#L54
            var x = b[3] << 24 | b[2] << 16 | b[1] << 8 | b[0];
            var y = (short)(b[5] << 8 | b[4]);
            var z = (short)(b[7] << 8 | b[6]);

            return new Guid(
                x, y, z,
                b[8], b[9], b[10],
                b[11], b[12], b[13],
                b[14], b[15]);
        }

        public static Guid Parse(ReadOnlySpan<char> input)
        {
            if (_parseShim != null)
            {
                return _parseShim(input);
            }

            var stringInput = StringHelper.Create(input);

            return Guid.Parse(stringInput);
        }

        public static Guid ParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format)
        {
            if (_parseExectShim != null)
            {
                return _parseExectShim(input, format);
            }

            var stringInput = StringHelper.Create(input);
            var stringFormat = StringHelper.Create(format);

            return Guid.ParseExact(stringInput, stringFormat);
        }


        public static bool TryParse(ReadOnlySpan<char> input, out Guid result)
        {
            if (_tryParseShim != null)
            {
                return _tryParseShim(input, out result);
            }

            var stringInput = StringHelper.Create(input);

            return Guid.TryParse(stringInput, out result);
        }

        public static bool TryParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format, out Guid result)
        {
            if (_tryParseExactShim != null)
            {
                return _tryParseExactShim(input, format, out result);
            }

            var stringInput = StringHelper.Create(input);
            var stringFormat = StringHelper.Create(format);

            return Guid.TryParseExact(stringInput, stringFormat, out result);
        }


    }
}
