/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

// Based on: http://www.singular.co.nz/2007/12/shortguid-a-shorter-and-url-friendly-guid-in-c-sharp/

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace AI4E
{
    /// <summary>
    /// Represents a globally unique identifier (GUID) with a
    /// shorter string value. Sguid
    /// </summary>
    [DebuggerDisplay("{Guid} ({Value})")]
    public struct SGuid : IEquatable<SGuid>
    {
        #region Fields

        /// <summary>
        /// Gets a read-only instance of the SGuid structure that represents an emtpy guid.
        /// </summary>
        public static SGuid Empty { get; } = new SGuid();

        private static readonly string _emptyValue = Encode(Guid.Empty);

        private readonly Guid _guid;
        private readonly string _value;

        #endregion

        #region Contructors

        /// <summary>
        /// Creates an <see cref="SGuid"/> from a base64 encoded <see cref="string"/>.
        /// </summary>
        /// <param name="value">The encoded guid as a base64 string.</param>
        public SGuid(string value)
        {
            _value = value;
            _guid = Decode(value);
        }

        /// <summary>
        /// Creates an <see cref="SGuid"/> from a <see cref="Guid"/>.
        /// </summary>
        /// <param name="guid">The Guid to encode.</param>
        public SGuid(Guid guid)
        {
            _value = Encode(guid);
            _guid = guid;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the underlying Guid.
        /// </summary>
        public Guid Guid => _guid;

        /// <summary>
        /// Gets the underlying base64 encoded string
        /// </summary>
        public string Value => _guid == Guid.Empty ? _emptyValue : _value;

        #endregion

        #region ToString

        /// <summary>
        /// Returns the base64 encoded guid as a string.
        /// </summary>
        /// <returns>The encoded guid as a base64 string.</returns>
        public override string ToString()
        {
            return Value;
        }

        #endregion

        #region Equals

        /// <summary>
        /// Returns a value indicating whether this instance and a
        /// specified Object represent the same type and value.
        /// </summary>
        /// <param name="obj">The object to compare</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is SGuid sguid && Equals(sguid);
        }

        public bool Equals(SGuid other)
        {
            return other._guid == _guid;
        }

        #endregion

        #region GetHashCode

        /// <summary>
        /// Returns the HashCode for underlying Guid.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }

        #endregion

        #region NewGuid

        /// <summary>
        /// Initialises a new instance of the ShortGuid class
        /// </summary>
        /// <returns></returns>
        public static SGuid NewGuid()
        {
            return new SGuid(Guid.NewGuid());
        }

        #endregion

        #region Encode

        /// <summary>
        /// Encodes the given Guid as a base64 string that is 22 characters long.
        /// </summary>
        /// <param name="guid">The Guid to encode.</param>
        /// <returns></returns>
        private static string Encode(Guid guid)
        {
            var encoded = Convert.ToBase64String(guid.ToByteArray());
            encoded = encoded
              .Replace("/", "_")
              .Replace("+", "-");
            return encoded.Substring(0, 22);
        }

        #endregion

        #region Decode

        /// <summary>
        /// Decodes the given base64 string.
        /// </summary>
        /// <param name="value">The base64 encoded string of a Guid</param>
        /// <returns>A new Guid</returns>
        private static Guid Decode(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            value = value
              .Replace("_", "/")
              .Replace("-", "+");
            var buffer = Convert.FromBase64String(value + "==");

            if (buffer.Length != 16)
                throw new FormatException("The value is not a valid base64 encoded guid.");

            return new Guid(buffer);
        }

        #endregion

        #region Operators

        /// <summary>
        /// Determines if both ShortGuids have the same underlying
        /// Guid value.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(SGuid left, SGuid right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines if both ShortGuids do not have the
        /// same underlying Guid value.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(SGuid left, SGuid right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Implicitly converts the ShortGuid to it's string equivilent
        /// </summary>
        /// <param name="shortGuid"></param>
        /// <returns></returns>
        public static implicit operator string(SGuid shortGuid)
        {
            return shortGuid._value;
        }

        /// <summary>
        /// Implicitly converts the ShortGuid to it's Guid equivilent
        /// </summary>
        /// <param name="shortGuid"></param>
        /// <returns></returns>
        public static implicit operator Guid(SGuid shortGuid)
        {
            return shortGuid._guid;
        }

        /// <summary>
        /// Implicitly converts the string to a ShortGuid
        /// </summary>
        /// <param name="shortGuid"></param>
        /// <returns></returns>
        public static implicit operator SGuid(string shortGuid)
        {
            return new SGuid(shortGuid);
        }

        /// <summary>
        /// Implicitly converts the Guid to a ShortGuid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static implicit operator SGuid(Guid guid)
        {
            return new SGuid(guid);
        }

        #endregion
    }

    public sealed class SGuidConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(SGuid) ||
                   sourceType == typeof(Guid) ||
                   sourceType == typeof(string) ||
                   base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(SGuid) ||
                   destinationType == typeof(Guid) ||
                   destinationType == typeof(string) ||
                   base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            switch (value)
            {
                case SGuid sguid:
                    return sguid;

                case Guid guid:
                    return new SGuid(guid);

                case string str:
                    return new SGuid(str);
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value is SGuid sguid)
            {
                if (destinationType == typeof(SGuid))
                    return sguid;

                if (destinationType == typeof(Guid))
                    return sguid.Guid;

                if (destinationType == typeof(string))
                    return sguid.Value;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
