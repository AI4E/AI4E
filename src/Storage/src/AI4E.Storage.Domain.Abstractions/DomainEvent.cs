﻿/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a domain-event raised on a domain entity.
    /// </summary>
    [TypeConverter(typeof(DomainEventTypeConverter))]
    public readonly struct DomainEvent : IEquatable<DomainEvent>
    {
        private static readonly object _emptyObject = new object();

        private readonly Type? _eventType;
        private readonly object? _event;

        public DomainEvent(object @event) : this(@event?.GetType(), @event) { }

        /// <summary>
        /// Creates a new instance of the <see cref="DomainEvent"/> type.
        /// </summary>
        /// <param name="eventType">The type of domain-event.</param>
        /// <param name="event">The domain-event object.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="eventType"/> or <paramref name="event"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="event"/> is not of type <paramref name="eventType"/> and cannot be assigned to it.
        /// </exception>
        public DomainEvent(Type eventType, object @event)
        {
            // Check event first, so that the correct argument exception is thrown for the ctor without eventType
            if (@event is null)
                throw new ArgumentNullException(nameof(@event));

            if (eventType is null)
                throw new ArgumentNullException(nameof(eventType));

            if (eventType.IsDelegate())
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyDelegateType, nameof(eventType));

            if (eventType.IsValueType)
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyValueType, nameof(eventType));

            if (eventType.IsInterface)
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyInterfaceType, nameof(eventType));

            if (eventType.IsGenericTypeDefinition)
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyOpenTypeDefinition, nameof(eventType));

            if (!eventType.IsAssignableFrom(@event.GetType()))
                throw new ArgumentException(Resources.EventMustBeAssignableToEventType, nameof(@event));

            if (@event.GetType().IsDelegate())
                throw new ArgumentException(Resources.ArgumentMustNotBeADelegate, nameof(@event));

            if (@event.GetType().IsValueType)
                throw new ArgumentException(Resources.ArgumentMustNotBeAValueType, nameof(@event));

            _eventType = eventType;
            _event = @event;
        }

        /// <summary>
        /// Gets the type of domain-event.
        /// </summary>
        public Type EventType => _eventType ?? typeof(object);

        /// <summary>
        /// Gets the domain-event object.
        /// </summary>
        public object Event => _event ?? _emptyObject;

        /// <summary>
        /// Deconstructs the current domain-event.
        /// </summary>
        /// <param name="eventType">Contains the type of domain-event.</param>
        /// <param name="event">Contains the domain-event object.</param>
        public void Deconstruct(out Type eventType, out object @event)
        {
            eventType = EventType;
            @event = Event;
        }

        /// <inheritdoc cref="IEquatable{DomainEvent}.Equals(DomainEvent)"/>
        public bool Equals(in DomainEvent other)
        {
            return EventType == other.EventType && EqualityComparer<object>.Default.Equals(Event, other.Event);
        }

        bool IEquatable<DomainEvent>.Equals(DomainEvent other)
        {
            return Equals(in other);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is DomainEvent domainEvent && Equals(in domainEvent);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(EventType, Event);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two domain-events are equal.
        /// </summary>
        /// <param name="left">The first <see cref="DomainEvent"/>.</param>
        /// <param name="right">The second <see cref="DomainEvent"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in DomainEvent left, in DomainEvent right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two domain-events are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="DomainEvent"/>.</param>
        /// <param name="right">The second <see cref="DomainEvent"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in DomainEvent left, in DomainEvent right)
        {
            return !left.Equals(in right);
        }
    }

    /// <summary>
    /// The type converter for the <see cref="DomainEvent"/> type.
    /// </summary>
    public sealed class DomainEventTypeConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(DomainEvent))
                return true;

            if (sourceType == typeof((Type, object)))
                return true;

            return base.CanConvertFrom(context, sourceType);
        }

        /// <inheritdoc/>
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(DomainEvent))
                return true;

            if (destinationType == typeof((Type, object)))
                return true;

            return base.CanConvertTo(context, destinationType);
        }

        /// <inheritdoc/>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value != null)
            {
                var sourceType = value.GetType();

                if (sourceType == typeof(DomainEvent))
                    return value;

                if (sourceType == typeof((Type, object)))
                {
                    var (eventType, @event) = ((Type eventType, object @event))value;
                    return new DomainEvent(eventType, @event);
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        /// <inheritdoc/>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(DomainEvent))
            {
                return value;
            }

            if (destinationType == typeof((Type, object)))
            {
                var (eventType, @event) = (DomainEvent)value;

                return (eventType, @event);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    /// <summary>
    /// Represents an immutable collection of domain-events.
    /// </summary>
    public readonly struct DomainEventCollection
        : IEquatable<DomainEventCollection>, IReadOnlyCollection<DomainEvent>
    {
        public static DomainEventCollection Empty { get; } = new DomainEventCollection();

        private readonly ImmutableHashSet<DomainEvent>? _domainEvents;

        /// <summary>
        /// Creates a new instance of type <see cref="DomainEventCollection"/> 
        /// from the specified collection of domain-events. 
        /// </summary>
        /// <param name="domainEvents">An <see cref="IEnumerable{DomainEvent}"/> enumerating the domain-events.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="domainEvents"/> is <c>null</c>.
        /// </exception>
        public DomainEventCollection(IEnumerable<DomainEvent> domainEvents)
        {
            if (domainEvents is null)
                throw new ArgumentNullException(nameof(domainEvents));

            _domainEvents = UncheckedBuildHashSet(domainEvents);
        }

        /// <summary>
        /// Creates a new instance of type <see cref="DomainEventCollection"/> 
        /// from the specified set of domain-events. 
        /// </summary>
        /// <param name="domainEvents">
        /// An <see cref="ImmutableHashSet{DomainEvent}"/> containing the domain-events.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="domainEvents"/> is <c>null</c>.
        /// </exception>
        public DomainEventCollection(ImmutableHashSet<DomainEvent> domainEvents)
        {
            if (domainEvents is null)
                throw new ArgumentNullException(nameof(domainEvents));

            _domainEvents = UncheckedBuildHashSet(domainEvents);
        }

        private static ImmutableHashSet<DomainEvent> UncheckedBuildHashSet(IEnumerable<DomainEvent> domainEvents)
        {
            if (domainEvents is ImmutableHashSet<DomainEvent> immutableHashSet)
            {
                return UncheckedBuildHashSet(immutableHashSet);
            }

            return domainEvents.ToImmutableHashSet();
        }

        private static ImmutableHashSet<DomainEvent> UncheckedBuildHashSet(ImmutableHashSet<DomainEvent> domainEvents)
        {
            if (domainEvents.KeyComparer == EqualityComparer<DomainEvent>.Default)
            {
                return domainEvents;
            }

            return domainEvents.WithComparer(EqualityComparer<DomainEvent>.Default);
        }

        /// <summary>
        /// Creates a new instance of type <see cref="DomainEventCollection"/>  from the specified domain-event. 
        /// </summary>
        /// <param name="domainEvent">
        /// The <see cref="DomainEvent"/> that is the single element in the collection.
        /// </param>
        public DomainEventCollection(DomainEvent domainEvent)
        {
            // TODO: Can we create a second field for this to prevent the allocation?
            _domainEvents = ImmutableHashSet.Create(domainEvent);
        }

        /// <inheritdoc />
        public int Count => _domainEvents?.Count ?? 0;

        public DomainEventCollection Add(DomainEvent domainEvent)
        {
            return Concat(new DomainEventCollection(domainEvent));
        }

        /// <summary>
        /// Concatenates the current <see cref="DomainEventCollection"/> with the specified one.
        /// </summary>
        /// <param name="other">The other <see cref="DomainEventCollection"/>.</param>
        /// <returns>
        /// A combined <see cref="DomainEventCollection"/> that contains all domain events from the current one 
        /// and <paramref name="other"/> but no duplicates.
        /// </returns>
        public DomainEventCollection Concat(DomainEventCollection other)
        {
            if (other._domainEvents is null || other.Count == 0)
                return this;

            if (_domainEvents is null || _domainEvents.Count == 0)
                return other;

            return new DomainEventCollection(_domainEvents.Union(other._domainEvents));
        }

        /// <inheritdoc />
        public bool Equals(DomainEventCollection other)
        {
            if (_domainEvents is null || _domainEvents.Count == 0)
                return other._domainEvents is null || other._domainEvents.Count == 0;

            if (other._domainEvents is null || other._domainEvents.Count == 0)
                return false;

            return _domainEvents.SetEquals(other._domainEvents);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is DomainEventCollection domainEvents && Equals(domainEvents);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var result = Count;

            foreach (var domainEvent in this)
            {
                // We use XOR here because the order of elements does not matter and we need to reflect this.
                result ^= domainEvent.GetHashCode();
            }

            return result;
        }

        /// <summary>
        /// Returns a boolean value indicating whether two domain-event collections are equal.
        /// </summary>
        /// <param name="left">The first <see cref="DomainEventCollection"/>.</param>
        /// <param name="right">The second <see cref="DomainEventCollection"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(DomainEventCollection left, DomainEventCollection right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two domain-event collections are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="DomainEventCollection"/>.</param>
        /// <param name="right">The second <see cref="DomainEventCollection"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(DomainEventCollection left, DomainEventCollection right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<DomainEvent> IEnumerable<DomainEvent>.GetEnumerator()
        {
            return (_domainEvents as IEnumerable<DomainEvent>)?.GetEnumerator()
                ?? Enumerable.Empty<DomainEvent>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (_domainEvents as IEnumerable)?.GetEnumerator() ?? Enumerable.Empty<DomainEvent>().GetEnumerator();
        }

        /// <summary>
        /// Represents an enumerator that enumerator through a <see cref="DomainEventCollection"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<DomainEvent>, IEnumerator
        {
            // This MUST NOT be marked read-only, to allow the compiler to access this field by reference.
            private ImmutableHashSet<DomainEvent>.Enumerator _underlying;
            private readonly bool _isInitialized;

            /// <summary>
            /// Creates a new instance of the <see cref="Enumerator"/> type enumerating 
            /// the specified <see cref="DomainEventCollection"/>.
            /// </summary>
            /// <param name="collection">The <see cref="DomainEventCollection"/> to enumerate.</param>
            public Enumerator(DomainEventCollection collection)
            {
                if (collection._domainEvents != null)
                {
                    _underlying = collection._domainEvents.GetEnumerator();
                    _isInitialized = true;
                }
                else
                {
                    _underlying = default;
                    _isInitialized = false;
                }
            }

            /// <inheritdoc />
            public DomainEvent Current => _isInitialized ? _underlying.Current : default;

            [ExcludeFromCodeCoverage]
            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_isInitialized)
                {
                    return _underlying.MoveNext();
                }

                return false;
            }

            /// <inheritdoc />
            public void Dispose() { }

            [ExcludeFromCodeCoverage]
            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }
        }
    }
}
