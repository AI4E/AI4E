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
using System.Collections.Generic;
using System.Diagnostics;

namespace AI4E.Domain
{
    #region Experimental

    public abstract class EntityBase : IEquatable<EntityBase>
    {
        private string _id;
        private readonly AggregateRootBase _aggregateRoot;
        private readonly Lazy<Type> _entityType;

        protected EntityBase(string id, AggregateRootBase aggregateRoot) : this(id)
        {
            if (aggregateRoot == null)
                throw new ArgumentNullException(nameof(aggregateRoot));

            _aggregateRoot = aggregateRoot;
        }

        private protected EntityBase(string id)
        {
            if (id == default)
                throw new ArgumentException("The id must not be an empty guid.", nameof(id));

            _id = id;
            _entityType = new Lazy<Type>(() => GetType());
        }

        protected internal string Id => _id;

        private protected Type EntityType => _entityType.Value;

        public AggregateRootBase AggregateRoot => GetAggregateRoot();

        private protected virtual AggregateRootBase GetAggregateRoot()
        {
            return _aggregateRoot;
        }

        protected virtual void Publish<TEvent>(TEvent evt)
            where TEvent : DomainEventBase
        {
            var aggregateRoot = GetAggregateRoot();

            Debug.Assert(aggregateRoot != null);

            aggregateRoot.Publish(evt);
        }

        public bool Equals(EntityBase other)
        {
            if (ReferenceEquals(other, null))
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
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);

            return left.Equals(right);
        }

        public static bool operator !=(EntityBase left, EntityBase right)
        {
            if (ReferenceEquals(left, null))
                return !ReferenceEquals(right, null);

            return !left.Equals(right);
        }
    }

    public abstract class AggregateRootBase : EntityBase
    {
        private bool _isDisposed;
        private readonly List<DomainEventBase> _uncommittedEvents = new List<DomainEventBase>();

        protected AggregateRootBase(string id) : base(id) { }

        public bool IsDisposed => _isDisposed;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                DoDispose();
                _isDisposed = true;
            }
        }

        public Guid ConcurrencyToken { get; internal set; }

        protected virtual void DoDispose() { }

        private protected sealed override AggregateRootBase GetAggregateRoot()
        {
            return this;
        }

        protected override void Publish<TEvent>(TEvent evt)
        {
            ThrowIfDisposed();

            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            if (evt.AggregateId != Id)
                throw new ArgumentException("The event does not belong to the stream of the aggregate.", nameof(evt));

            _uncommittedEvents.Add(evt);
        }

        internal IEnumerable<DomainEventBase> UncommittedEvents => _uncommittedEvents;

        internal void CommitEvents()
        {
            _uncommittedEvents.Clear();
        }

        protected virtual void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(EntityType.FullName);
        }

        protected T ThrowIfDisposed<T>(T value)
        {
            ThrowIfDisposed();
            return value;
        }

        protected T ThrowIfDisposed<T>(Func<T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            ThrowIfDisposed();
            return factory();
        }
    }

    public abstract class DomainEventBase
    {
        public DomainEventBase(string aggregateId)
        {
            AggregateId = aggregateId;
        }

        protected internal string AggregateId { get; }
    }

    public abstract class Entity<TId> : EntityBase
        where TId : struct, IEquatable<TId>
    {
        protected Entity(TId id, AggregateRootBase aggregateRoot) : base(id.ToString(), aggregateRoot)
        {
            if (aggregateRoot == null)
                throw new ArgumentNullException(nameof(aggregateRoot));

            Id = id;
        }

        private protected Entity(TId id) : base(id.ToString())
        {
            Id = id;
        }

        public new TId Id { get; }
    }

    public abstract class AggregateRoot<TId> : AggregateRootBase
        where TId : struct, IEquatable<TId>
    {
        protected AggregateRoot(TId id) : base(id.ToString())
        {
            Id = id;
        }

        public new TId Id { get; }
    }

    public abstract class DomainEvent<TId> : DomainEventBase
        where TId : struct, IEquatable<TId>
    {
        public DomainEvent(TId aggregateId) : base(aggregateId.ToString())
        {
            AggregateId = aggregateId;
        }

        public new TId AggregateId { get; }
    }

    #endregion
}
