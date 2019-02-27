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

using System;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;

namespace AI4E.Modularity
{
    // A handle for a module (f.e. AI4E.Clustering)
    [TypeConverter(typeof(ModuleIdentifierTypeConverter))]
    public readonly struct ModuleIdentifier : IEquatable<ModuleIdentifier>
    {
        public static ModuleIdentifier UnknownModule { get; } = new ModuleIdentifier();

        public ModuleIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The argument must neither be null nor an empty string nor a string that consists of whitespace only.", nameof(name));

            Name = name;
        }

        [JsonProperty]
        public string Name { get; }

        public bool Equals(ModuleIdentifier other)
        {
            return other.Name == Name;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleIdentifier module && Equals(module);
        }

        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            if (this == UnknownModule)
                return "Unknown module";

            return Name;
        }

        public static bool operator ==(ModuleIdentifier left, ModuleIdentifier right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleIdentifier left, ModuleIdentifier right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class ModuleIdentifierTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(ModuleIdentifier) ||
                   sourceType == typeof(ModuleIdentifier?) ||
                   sourceType == typeof(string) ||
                   base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(ModuleIdentifier) ||
                   destinationType == typeof(ModuleIdentifier?) ||
                   destinationType == typeof(string) ||
                   base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is ModuleIdentifier identifier)
            {
                return value;
            }
            else if (value is ModuleIdentifier?)
            {
                return ((ModuleIdentifier?)value).Value;
            }
            else if (value is string str)
            {
                return new ModuleIdentifier(str);
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value is ModuleIdentifier identifier)
            {
                if (destinationType == typeof(ModuleIdentifier))
                {
                    return identifier;
                }
                else if (destinationType == typeof(ModuleIdentifier?))
                {
                    return new ModuleIdentifier?(identifier);
                }
                else if (destinationType == typeof(string))
                {
                    return identifier.Name;
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
