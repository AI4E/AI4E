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

namespace AI4E.Modularity.Host
{
    public readonly struct ModuleSourceName : IEquatable<ModuleSourceName>
    {
        public ModuleSourceName(string value)
        {
            if (!IsValid(value, out var message))
                throw new ArgumentException(message, nameof(value));

            Value = value;
        }

        public string Value { get; }

        public bool Equals(ModuleSourceName other)
        {
            return other.Value == Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleSourceName moduleSourceName && Equals(moduleSourceName);
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        public static bool operator ==(in ModuleSourceName left, in ModuleSourceName right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ModuleSourceName left, in ModuleSourceName right)
        {
            return !left.Equals(right);
        }

        public static bool IsValid(string value, out string message)
        {
            if(string.IsNullOrWhiteSpace(value))
            {
                message = "The module source name must not be empty.";
                return false;
            }

            message = default;
            return true;
        }

        public static explicit operator ModuleSourceName(string name)
        {
            return new ModuleSourceName(name);
        }
    }
}
