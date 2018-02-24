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
using AI4E.Storage;

namespace AI4E
{
    public abstract class CommandHandler<TId, TEventBase, TEntityBase, TEntity> : MessageHandler
        where TId : struct, IEquatable<TId>
        where TEventBase : class
        where TEntityBase : class
        where TEntity : class, TEntityBase
    {
        [CommandHandlerEntityDeleteFlag]
        public bool IsMarkedAsDeleted { get; internal set; }

        [CommandHandlerEntity]
        public TEntity Entity { get; set; }

        [CommandHandlerEntityStore]
        public IEntityStore<TId, TEventBase, TEntityBase> EntityStore { get; internal set; }

        [NoAction]
        protected void MarkAsDeleted()
        {
            IsMarkedAsDeleted = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class CommandHandlerEntityAttribute : Attribute
    {
        public Type EntityType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class CommandHandlerEntityStoreAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class CommandHandlerEntityDeleteFlagAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CreatesEntityAttribute : Attribute
    {
        public CreatesEntityAttribute() : this(true) { }

        public CreatesEntityAttribute(bool createsEntity)
        {
            CreatesEntity = createsEntity;
        }

        public bool CreatesEntity { get; }

        public bool AllowExisingEntity { get; set; } = false;
    }
}
