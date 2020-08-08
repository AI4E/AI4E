/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
    /// <summary>
    /// An abstract base class that can be used to implement message handlers that manage an entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <remarks>
    /// This defines a base type that can be used to consume and manipulate entities in a message handler.
    /// For the actual implementation (loading/storing the entity etc.) see EntityMessageHandlerProcessor
    /// </remarks>
    public abstract class MessageHandler<TEntity> : MessageHandler
        where TEntity : class
    {
        /// <summary>
        /// Gets a boolean value indicating whether the entity is marked as deleted.
        /// </summary>
        [MessageHandlerEntityDeleteFlag]
        public bool IsMarkedAsDeleted { get; internal set; }

        /// <summary>
        /// Gets or sets the entity.
        /// </summary>
        [MessageHandlerEntity]
#nullable disable annotations
        public TEntity Entity { get; set; }
#nullable enable annotations

        /// <summary>
        /// Marks the entity as deleted.
        /// </summary>
        [NoMessageHandler]
        protected void MarkAsDeleted()
        {
            IsMarkedAsDeleted = true;
        }
    }

    /// <summary>
    /// Marks a property of a message handler to contain the managed entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerEntityAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the type of handled entity or <c>null</c> if the property type specifies the type of entity.
        /// </summary>
        public Type? EntityType { get; set; }
    }

    /// <summary>
    /// Marks the boolean property that indicates whether the entity is marked as deleted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerEntityDeleteFlagAttribute : Attribute { }

    /// <summary>
    /// Configures a message handler action method's capability to handle non-existing entities.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class CreatesEntityAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance of the <see cref="CreatesEntityAttribute"/> attribute that 
        /// indicates entity create ability.
        /// </summary>
        public CreatesEntityAttribute() : this(true) { }

        /// <summary>
        /// Creates a new instance of the <see cref="CreatesEntityAttribute"/> attribute.
        /// </summary>
        /// <param name="createsEntity">A boolean value indicating entity create ability.</param>
        public CreatesEntityAttribute(bool createsEntity)
        {
            CreatesEntity = createsEntity;
        }

        /// <summary>
        /// Gets a boolean value indicating entity create ability.
        /// </summary>
        public bool CreatesEntity { get; }

        /// <summary>
        /// Gets a boolean value indicating whether an existing entity is allowed.
        /// </summary>
        public bool AllowExisingEntity { get; set; } = false;
    }

    /// <summary>
    /// Marks a method that is used as a custom entity lookup.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class EntityLookupAttribute : Attribute { }
}
