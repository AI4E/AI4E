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

namespace AI4E.Domain
{
    public abstract class EntityBase : IEquatable<EntityBase>
    {
        private readonly Lazy<Type> _entityType;

        protected EntityBase(string id)
        {
            if (id == default)
                throw new ArgumentException("The id must not be an empty guid.", nameof(id));

            Id = id;
            _entityType = new Lazy<Type>(() => GetType());
        }

        protected internal string Id { get; }

        private protected Type EntityType => _entityType.Value;

        public bool Equals(EntityBase other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(other, this))
                return true;

            return Equals(other.EntityType == EntityType && other.Id == Id);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EntityBase);
        }

        public override int GetHashCode()
        {
            return EntityType.GetHashCode() ^ Id.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{{EntityType.FullName} #{Id}}}";
        }

        public static bool operator ==(EntityBase left, EntityBase right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        public static bool operator !=(EntityBase left, EntityBase right)
        {
            if (left is null)
                return !(right is null);

            return !left.Equals(right);
        }
    }
}
