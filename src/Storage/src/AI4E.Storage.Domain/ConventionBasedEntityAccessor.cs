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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using AI4E.Utils;

namespace AI4E.Storage.Domain
{
    internal sealed class ConventionBasedEntityAccessor
    {
        private const BindingFlags _defaultBinding 
            = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        #region Caching

        private static readonly ConditionalWeakTable<Type, ConventionBasedEntityAccessor> _instances
            = new ConditionalWeakTable<Type, ConventionBasedEntityAccessor>();

        // Cache delegate for perf reasons
        private static readonly ConditionalWeakTable<Type, ConventionBasedEntityAccessor>.CreateValueCallback _buildInstance
            = BuildInstance;

        private static ConventionBasedEntityAccessor BuildInstance(Type entityType)
        {
            return new ConventionBasedEntityAccessor(entityType);
        }

        public static ConventionBasedEntityAccessor GetAccessor(Type entityType)
        {
            return _instances.GetValue(entityType, _buildInstance);
        }

        #endregion

        private readonly Func<object, string?>? _idGetAccessor;
        private readonly Action<object, string>? _idSetAccessor;
        private readonly Func<object, string?>? _concurrencyTokenGetAccessor;
        private readonly Action<object, string>? _concurrencyTokenSetAccessor;
        private readonly Func<object, long>? _revisionGetAccessor;
        private readonly Action<object, long>? _revisionSetAccessor;
        private readonly Func<object, IEnumerable<DomainEvent>>? _uncommittedEventsGetAccessor;
        private readonly Action<object>? _commitEventsAccessor;
        private readonly Action<object, DomainEvent>? _addEventAccessor;

        private ConventionBasedEntityAccessor(Type entityType)
        {
            EntityType = entityType;

            CanAccessId = BuildIdAccessors(
                out _idGetAccessor, out _idSetAccessor);

            CanAccessConcurrencyToken = BuildConcurrencyTokenAccessors(
                out _concurrencyTokenGetAccessor, out _concurrencyTokenSetAccessor);

            CanAccessRevision = BuildRevisionAccessors(
                out _revisionGetAccessor, out _revisionSetAccessor);

            CanAccessEvents = BuildEventsAccessors(
                out _uncommittedEventsGetAccessor,
                out _commitEventsAccessor,
                out _addEventAccessor);
        }

        public Type EntityType { get; }

        public bool CanAccessId { get; }
        public bool CanAccessConcurrencyToken { get; }
        public bool CanAccessRevision { get; }
        public bool CanAccessEvents { get; }

        public string? GetId(object entity)
        {
            Debug.Assert(CanAccessId);
            return _idGetAccessor!.Invoke(entity);
        }

        public void SetId(object entity, string id)
        {
            Debug.Assert(CanAccessId);
            _idSetAccessor!.Invoke(entity, id);
        }

        public ConcurrencyToken GetConcurrencyToken(object entity)
        {
            Debug.Assert(CanAccessConcurrencyToken);
            return _concurrencyTokenGetAccessor!.Invoke(entity);
        }

        public void SetConcurrencyToken(object entity, ConcurrencyToken concurrencyToken)
        {
            Debug.Assert(CanAccessConcurrencyToken);
            _concurrencyTokenSetAccessor!.Invoke(entity, concurrencyToken.ToString());
        }

        public long GetRevision(object entity)
        {
            Debug.Assert(CanAccessRevision);
            return _revisionGetAccessor!.Invoke(entity);
        }

        public void SetRevision(object entity, long revision)
        {
            Debug.Assert(CanAccessRevision);
            _revisionSetAccessor!.Invoke(entity, revision);
        }

        public void CommitEvents(object entity)
        {
            Debug.Assert(CanAccessEvents);
            _commitEventsAccessor!.Invoke(entity);
        }

        public DomainEventCollection GetUncommittedEvents(object entity)
        {
            Debug.Assert(CanAccessEvents);
            return new DomainEventCollection(_uncommittedEventsGetAccessor!.Invoke(entity));
        }

        public void AddEvent(object entity, DomainEvent domainEvent)
        {
            Debug.Assert(CanAccessEvents);
            _addEventAccessor!.Invoke(entity, domainEvent);
        }

        #region BuildAccessors

        private bool BuildIdAccessors(
            [NotNullWhen(true)] out Func<object, string?>? idGetAccessor,
            [NotNullWhen(true)] out Action<object, string>? idSetAccessor)
        {
            var property = EntityType.GetProperty(
                nameof(DomainEntity.Id),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null || !property.CanRead || !property.CanWrite)
            {
                idGetAccessor = null;
                idSetAccessor = null;
                return false;
            }

            return BuildAccessorWithConversion(
                out idGetAccessor,
                out idSetAccessor,
                property);
        }

        private bool BuildConcurrencyTokenAccessors(
            [NotNullWhen(true)] out Func<object, string?>? concurrencyTokenGetAccessor,
            [NotNullWhen(true)] out Action<object, string>? concurrencyTokenSetAccessor)
        {
            var property = EntityType.GetProperty(
                nameof(DomainEntity.ConcurrencyToken),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null || !property.CanRead || !property.CanWrite)
            {
                concurrencyTokenGetAccessor = null;
                concurrencyTokenSetAccessor = null;
                return false;
            }

            return BuildAccessorWithConversion(
                out concurrencyTokenGetAccessor,
                out concurrencyTokenSetAccessor,
                property);
        }

        private bool BuildRevisionAccessors(
            [NotNullWhen(true)] out Func<object, long>? revisionGetAccessor,
            [NotNullWhen(true)] out Action<object, long>? revisionSetAccessor)
        {
            revisionGetAccessor = null;
            revisionSetAccessor = null;

            var revisionProperty = EntityType.GetProperty(
               nameof(DomainEntity.Revision),
               BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (revisionProperty == null
                || !revisionProperty.CanRead
                || !revisionProperty.CanWrite
                || revisionProperty.PropertyType != typeof(long))
            {
                return false;
            }

            var entityParameter = Expression.Parameter(typeof(object), "entity");
            var convertedEntity = Expression.Convert(entityParameter, EntityType);
            var propertyAccess = Expression.MakeMemberAccess(convertedEntity, revisionProperty);

            // Get accessor
            revisionGetAccessor = Expression.Lambda<Func<object, long>>(propertyAccess, entityParameter).Compile();

            // Set accessor
            var revisionParameter = Expression.Parameter(typeof(long), "revision");
            var assignment = Expression.Assign(propertyAccess, revisionParameter);

            revisionSetAccessor = Expression.Lambda<Action<object, long>>(assignment, entityParameter, revisionParameter).Compile();

            return true;
        }

        private bool BuildEventsAccessors(
            [NotNullWhen(true)] out Func<object, IEnumerable<DomainEvent>>? uncommittedEventsGetAccessor,
            [NotNullWhen(true)] out Action<object>? commitEventsAccessor,
            [NotNullWhen(true)] out Action<object, DomainEvent>? addEventAccessor)
        {
            uncommittedEventsGetAccessor = null;
            commitEventsAccessor = null;
            addEventAccessor = null;

            if (!TryGetCommitEventsMethod(out var commitEventsMethod, out var addionalCommitEventsArguments))
            {
                return false;
            }

            if (!TryGetEventsMethods(
                out var addEventMethod,
                out var additionalAddEventArguments,
                out var getUncommitedEventsMethod,
                out var additionalGetUncommitedEventsArguments,
                out var domainEventType))
            {
                return false;
            }

            // TODO: Support conversion if any of the types (source/dest) has a converter plus additional conversion methods.
            TypeConverter? converter = null;

            if (domainEventType != typeof(DomainEvent))
            {
                converter = TypeDescriptor.GetConverter(typeof(DomainEvent));

                if (!converter.CanConvertFrom(domainEventType) || !converter.CanConvertTo(domainEventType))
                {
                    return false;
                }
            }

            var entityParameter = Expression.Parameter(typeof(object), "entity");
            var convertedEntity = Expression.Convert(entityParameter, EntityType);

            // commitEventsAccessor
            var commitEventsCall = addionalCommitEventsArguments is null ?
                Expression.Call(convertedEntity, commitEventsMethod) :
                Expression.Call(convertedEntity, commitEventsMethod, addionalCommitEventsArguments);

            commitEventsAccessor = Expression.Lambda<Action<object>>(commitEventsCall, entityParameter).Compile();

            // uncommittedEventsGetAccessor
            var uncommittedEventsGetCall = additionalGetUncommitedEventsArguments is null ?
                Expression.Call(convertedEntity, getUncommitedEventsMethod) :
                Expression.Call(convertedEntity, getUncommitedEventsMethod, additionalGetUncommitedEventsArguments);

            if (converter is null)
            {
                var convertedUncommittedEventsGetCall = Expression.Convert(uncommittedEventsGetCall, typeof(IEnumerable<DomainEvent>));
                uncommittedEventsGetAccessor = Expression.Lambda<Func<object, IEnumerable<DomainEvent>>>(convertedUncommittedEventsGetCall, entityParameter).Compile();
            }
            else
            {
                var convertedUncommittedEventsGetCall = Expression.Convert(uncommittedEventsGetCall, typeof(IEnumerable));
                var unconvertedEventsGetAccessor = Expression.Lambda<Func<object, IEnumerable>>(convertedUncommittedEventsGetCall, entityParameter).Compile();

                IEnumerable<DomainEvent> ExecuteConvertedUncommittedEventsGetCallAndConvertEvents(object entity)
                {
                    var unconvertedEvents = unconvertedEventsGetAccessor(entity);
                    var result = new List<DomainEvent>();

                    foreach (var evt in unconvertedEvents)
                    {
                        result.Add((DomainEvent)converter!.ConvertFrom(evt));
                    }

                    return result;
                }

                uncommittedEventsGetAccessor = ExecuteConvertedUncommittedEventsGetCallAndConvertEvents;
            }

            // addEventAccessor
            static Expression[] Combine(Expression exp, Expression[] additional)
            {
                var result = new Expression[additional.Length + 1];
                result[0] = exp;
                Array.Copy(additional, 0, result, 1, additional.Length);
                return result;
            }

            if (converter is null)
            {
                var domainEventParameter = Expression.Parameter(typeof(DomainEvent), "domainEvent");
                var addEventCall = additionalAddEventArguments is null ?
                    Expression.Call(convertedEntity, addEventMethod, domainEventParameter) :
                    Expression.Call(convertedEntity, addEventMethod, Combine(domainEventParameter, additionalAddEventArguments));

                addEventAccessor = Expression.Lambda<Action<object, DomainEvent>>(addEventCall, entityParameter, domainEventParameter).Compile();
            }
            else
            {
                var domainEventParameter = Expression.Parameter(typeof(object), "domainEvent");
                var convertedDomainEventParameter = Expression.Convert(domainEventParameter, domainEventType);
                var addEventCall = additionalAddEventArguments is null ?
                    Expression.Call(convertedEntity, addEventMethod, convertedDomainEventParameter) :
                    Expression.Call(convertedEntity, addEventMethod, Combine(convertedDomainEventParameter, additionalAddEventArguments));

                var convertedEventAddEventAccessor = Expression.Lambda<Action<object, object>>(addEventCall, entityParameter, domainEventParameter).Compile();

                void ConvertDomainEventAndExecuteAddEventAccessor(object entity, DomainEvent domainEvent)
                {
                    var convertedEvent = converter!.ConvertTo(domainEvent, domainEventType);
                    convertedEventAddEventAccessor(entity, convertedEvent);
                }

                addEventAccessor = ConvertDomainEventAndExecuteAddEventAccessor;
            }

            return true;
        }

        private bool TryGetEventsMethods(
            [NotNullWhen(true)] out MethodInfo? addEventMethod,
             out ConstantExpression[]? additionalAddEventArguments,
            [NotNullWhen(true)] out MethodInfo? getUncommitedEventsMethod,
            out ConstantExpression[]? additionalGetUncommitedEventsArguments,
            [NotNullWhen(true)] out Type? domainEventType)
        {
            var uncommitedEventsMethods = GetUncommitedEventsMethods().Cached();

            foreach (var method in EntityType.GetMethods(_defaultBinding))
            {
                if (method.Name != nameof(DomainEntity.AddEvent) || method.IsGenericMethodDefinition)
                    continue;

                additionalAddEventArguments = null;
                var parameters = method.GetParameters();

                if (parameters.Length == 0)
                    continue;

                if (parameters.Length > 1
                    && !TryGetAdditionalArguments(parameters.AsSpan().Slice(1), out additionalAddEventArguments))
                    continue;

                var firstParameter = parameters.First();

                if (firstParameter.ParameterType.IsByRef)
                    continue;

                domainEventType = firstParameter.ParameterType;
                var enumerableOfDomainEventsType = typeof(IEnumerable<>).MakeGenericType(domainEventType);

                (getUncommitedEventsMethod, additionalGetUncommitedEventsArguments)
                    = uncommitedEventsMethods.FirstOrDefault(
                        p => enumerableOfDomainEventsType.IsAssignableFrom(p.method.ReturnType));

                if (getUncommitedEventsMethod != null)
                {
                    addEventMethod = method;
                    return true;
                }
            }

            addEventMethod = null;
            additionalAddEventArguments = null;
            getUncommitedEventsMethod = null;
            additionalGetUncommitedEventsArguments = null;
            domainEventType = null;
            return false;
        }

        private IEnumerable<(MethodInfo method, ConstantExpression[]? additionalArguments)> GetUncommitedEventsMethods()
        {
            var methods = EntityType
                .GetMethods(_defaultBinding)
                .Where(p => p.Name == nameof(DomainEntity.GetUncommittedEvents) && !p.IsGenericMethodDefinition)
                .ToArray();

            foreach (var method in methods)
            {
                if (!TryGetAdditionalArguments(method.GetParameters(), out var additionalArguments))
                    continue;

                yield return (method, additionalArguments);
            }
        }

        private bool TryGetCommitEventsMethod(
            [NotNullWhen(true)] out MethodInfo? commitEventsMethod,
            out ConstantExpression[]? additionalArguments)
        {
            foreach (var method in EntityType.GetMethods(_defaultBinding))
            {
                if (method.Name != nameof(DomainEntity.CommitEvents) || method.IsGenericMethodDefinition)
                    continue;

                if (!TryGetAdditionalArguments(method.GetParameters(), out additionalArguments))
                {
                    continue;
                }

                commitEventsMethod = method;
                return true;
            }

            commitEventsMethod = null;
            additionalArguments = null;
            return false;
        }

        private static bool TryGetAdditionalArguments(
            Span<ParameterInfo> parameters,
            out ConstantExpression[]? additionalArguments)
        {
            additionalArguments = null;

            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType.IsByRef)
                {
                    additionalArguments = null;
                    return false;
                }

                if (!ParameterDefaultValue.TryGetDefaultValue(parameters[i], out var defaultValue))
                {
                    additionalArguments = null;
                    return false;
                }

                additionalArguments ??= new ConstantExpression[parameters.Length];
                additionalArguments[i] = Expression.Constant(
                    defaultValue, parameters[i].ParameterType);
            }

            return true;
        }

        private bool BuildAccessorWithConversion(
            [NotNullWhen(true)] out Func<object, string?>? getAccessor,
            [NotNullWhen(true)] out Action<object, string>? setAccessor,
            PropertyInfo property)
        {
            var propertyType = property.PropertyType;
            TypeConverter? converter = null;

            if (propertyType != typeof(string))
            {
                converter = TypeDescriptor.GetConverter(propertyType);

                if (!converter.CanConvertFrom(typeof(string)) || !converter.CanConvertTo(typeof(string)))
                {
                    getAccessor = null;
                    setAccessor = null;
                    return false;
                }
            }

            var entityParameter = Expression.Parameter(typeof(object), "entity");
            var convertedEntity = Expression.Convert(entityParameter, EntityType);
            var propertyAccess = Expression.MakeMemberAccess(convertedEntity, property);

            // Get accessor
            if (converter is null)
            {
                getAccessor = Expression.Lambda<Func<object, string?>>(propertyAccess, entityParameter).Compile();
            }
            else
            {
                var convertedResult = Expression.Convert(propertyAccess, typeof(object));
                var unconvertedValueAccessor = Expression.Lambda<Func<object, object>>(convertedResult, entityParameter).Compile();

                string? ExecuteGetAccessorAndConvert(object entity)
                {
                    var unconvertedResult = unconvertedValueAccessor(entity);

                    if (IsDefaultValue(propertyType, unconvertedResult))
                        return null;

                    return converter!.ConvertTo(unconvertedResult, typeof(string)) as string;
                }

                getAccessor = ExecuteGetAccessorAndConvert;
            }

            // Set accessor
            if (converter is null)
            {
                var parameter = Expression.Parameter(typeof(string));
                var assignment = Expression.Assign(propertyAccess, parameter);

                setAccessor = Expression.Lambda<Action<object, string>>(assignment, entityParameter, parameter).Compile();
            }
            else
            {
                var parameter = Expression.Parameter(typeof(object));
                var converted = Expression.Convert(parameter, propertyType);
                var assignment = Expression.Assign(propertyAccess, converted);
                var convertedValueAccessor = Expression.Lambda<Action<object, object>>(assignment, entityParameter, parameter).Compile();

                void ConvertAndExecuteSetAccessor(object entity, string value)
                {
                    var convertedValue = converter!.ConvertFrom(value);

                    convertedValueAccessor(entity, convertedValue);
                }

                setAccessor = ConvertAndExecuteSetAccessor;
            }

            return true;
        }

        // TODO: Can we speed this up? This is executed on the hot path.
        private static bool IsDefaultValue(Type type, object value)
        {
            if (value is null)
                return true;

            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = type.GetGenericArguments().First();
            }

            if (!type.IsValueType)
                return false;

            return EqualityComparer<object>.Default.Equals(value, Activator.CreateInstance(type));
        }

        #endregion
    }
}
