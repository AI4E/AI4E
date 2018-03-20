/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        Entity.cs 
 * Types:           (1) AI4E.Domain.Entity
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   18.10.2017 
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using System.Diagnostics;

namespace AI4E.Domain
{
    public abstract class Entity : IEquatable<Entity>
    {
        private Guid _id;
        private readonly Lazy<Type> _entityType;

        protected Entity(Guid id)
        {
            if (id == default)
                throw new ArgumentException("The id must not be an empty guid.", nameof(id));

            _id = id;
            _entityType = new Lazy<Type>(() => GetType());
        }

        public virtual Guid Id
        {
            get => _id;
            internal set => _id = value;
        }

        private protected Type EntityType => _entityType.Value;

        public bool Equals(Entity other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (ReferenceEquals(other, this))
                return true;

            return Equals(other.EntityType == EntityType && other.Id == Id);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Entity);
        }

        public override int GetHashCode()
        {
            return EntityType.GetHashCode() ^ Id.GetHashCode();
        }

        public override string ToString()
        {
            return $"{{{EntityType.FullName} #{Id}}}";
        }

        public static bool operator ==(Entity left, Entity right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);

            return left.Equals(right);
        }

        public static bool operator !=(Entity left, Entity right)
        {
            if (ReferenceEquals(left, null))
                return !ReferenceEquals(right, null);

            return !left.Equals(right);
        }
    }
}
