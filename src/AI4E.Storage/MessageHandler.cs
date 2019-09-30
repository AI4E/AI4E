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

namespace AI4E.Messaging
{
    // This defined a base type that can be used to consume entities in a message handler.
    // For the actual implementation (loading/storing the entity etc.) see EntityMessageHandlerProcessor
    public abstract class MessageHandler<TEntity> : MessageHandler
        where TEntity : class
    {
        [MessageHandlerEntityDeleteFlag]
        public bool IsMarkedAsDeleted { get; internal set; }

        [MessageHandlerEntity]
        public TEntity Entity { get; set; }

        [NoMessageHandler]
        protected void MarkAsDeleted()
        {
            IsMarkedAsDeleted = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerEntityAttribute : Attribute
    {
        public Type EntityType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerEntityDeleteFlagAttribute : Attribute { }

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

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class EntityLookupAttribute : Attribute { }
}
