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
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a type descriptor for entity managing message handlers.
    /// </summary>
    public sealed class EntityMessageHandlerContextDescriptor
    {
        private const BindingFlags _defaultBinding
            = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        #region Caching

        private static readonly ConcurrentDictionary<Type, EntityMessageHandlerContextDescriptor?> _descriptors
           = new ConcurrentDictionary<Type, EntityMessageHandlerContextDescriptor?>();

        private static readonly Func<Type, EntityMessageHandlerContextDescriptor?> _buildDescriptor
            = BuildDescriptor;

        /// <summary>
        /// Tries to retrieve the <see cref="EntityMessageHandlerContextDescriptor"/> for the specified type.
        /// </summary>
        /// <param name="handlerType">The type of message handlers.</param>
        /// <param name="descriptor">
        /// Contains the <see cref="EntityMessageHandlerContextDescriptor"/> if it can be retrieved.
        /// </param>
        /// <returns>True if the descriptor can be retrieved, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="handlerType"/> is <c>null</c>.
        /// </exception>
        public static bool TryGetDescriptor(
            Type handlerType,
            [NotNullWhen(true)] out EntityMessageHandlerContextDescriptor? descriptor)
        {
            if (handlerType == null)
                throw new ArgumentNullException(nameof(handlerType));

            descriptor = _descriptors.GetOrAdd(handlerType, _buildDescriptor);
            return descriptor != null;
        }

        private static EntityMessageHandlerContextDescriptor? BuildDescriptor(Type handlerType)
        {
            if (TryGetEntityProperty(handlerType, out var entityProperty))
            {
                return new EntityMessageHandlerContextDescriptor(handlerType, entityProperty);
            }

            return null;
        }

        #endregion

        #region Build

        // Cache delegate for perf reasons.
        private static readonly Func<PropertyInfo, bool> _isEntityProperty = IsEntityProperty;

        private static bool IsEntityProperty(PropertyInfo property)
        {
            if (property.PropertyType.IsValueType)
                return false;

            if (!property.CanRead || !property.CanWrite)
                return false;

            if (property.GetIndexParameters().Length != 0)
                return false;

            return property.IsDefined<MessageHandlerEntityAttribute>();
        }

        private static bool TryGetEntityProperty(Type handlerType, [NotNullWhen(true)] out PropertyInfo? entityProperty)
        {
            entityProperty = null;

#pragma warning disable IDE0007
            for (Type? current = handlerType; current != null; current = current.BaseType)
#pragma warning restore IDE0007
            {
                entityProperty = handlerType.GetProperties(_defaultBinding).FirstOrDefault(_isEntityProperty);

                if (entityProperty != null)
                    return true;
            }

            return false;
        }

        private static void BuildEntityAccessor(
            Type handlerType,
            PropertyInfo entityProperty,
            out Func<object, object?> entityGetter,
            out Action<object, object?> entitySetter)
        {
            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var entityParam = Expression.Parameter(typeof(object), "entity");
            var convertedHandler = Expression.Convert(handlerParam, handlerType);
            var convertedEntity = Expression.TypeAs(entityParam, entityProperty!.PropertyType);
            var propertyAccess = Expression.Property(convertedHandler, entityProperty);
            var propertyAssign = Expression.Assign(propertyAccess, convertedEntity);
            var getterLambda = Expression.Lambda<Func<object, object?>>(propertyAccess, handlerParam);
            var setterLambda = Expression.Lambda<Action<object, object?>>(propertyAssign, handlerParam, entityParam);

            entityGetter = getterLambda.Compile();
            entitySetter = setterLambda.Compile();
        }

        private static Type GetEntityType(PropertyInfo entityProperty)
        {
            Debug.Assert(entityProperty != null);

            var result = entityProperty!.PropertyType;
            var customType = entityProperty.GetCustomAttribute<MessageHandlerEntityAttribute>()?.EntityType;

            if (customType != null &&
                result.IsAssignableFrom(customType)) // If the types do not match, we just ignore the custom type.
            {
                result = customType;
            }

            return result;
        }

        // Cache delegate for perf reasons.
        private static readonly Func<PropertyInfo, bool> _isDeleteFlagAccessor = IsDeleteFlagAccessor;

        private static bool IsDeleteFlagAccessor(PropertyInfo property)
        {
            if (property.PropertyType != typeof(bool))
                return false;

            if (!property.CanRead)
                return false;

            if (property.GetIndexParameters().Length != 0)
                return false;

            return property.IsDefined<MessageHandlerEntityDeleteFlagAttribute>();
        }

        private static bool TryGetDeleteFlagProperty(
            Type handlerType,
            [NotNullWhen(true)] out PropertyInfo? deleteFlagProperty)
        {
            deleteFlagProperty = null;

#pragma warning disable IDE0007
            for (Type? current = handlerType; current != null; current = current.BaseType)
#pragma warning restore IDE0007
            {
                deleteFlagProperty = handlerType.GetProperties(_defaultBinding).FirstOrDefault(_isDeleteFlagAccessor);

                if (deleteFlagProperty != null)
                    return true;
            }

            return false;
        }

        private static Func<object, bool>? BuildDeleteFlagAccessor(Type handlerType)
        {
            if (!TryGetDeleteFlagProperty(handlerType, out var deleteFlagProperty))
            {
                return null;
            }

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var convertedHandler = Expression.Convert(handlerParam, handlerType);
            var deleteFlagPropertyAccess = Expression.Property(convertedHandler, deleteFlagProperty);
            var deleteFlagLambda = Expression.Lambda<Func<object, bool>>(deleteFlagPropertyAccess, handlerParam);

            return deleteFlagLambda.Compile();
        }

        private static ImmutableDictionary<Type, LookupAccessor> BuildLookupAccessors(Type handlerType, Type entityType)
        {
            var methods = handlerType.GetMethods(_defaultBinding);
            var resultBuilder = ImmutableDictionary.CreateBuilder<Type, LookupAccessor>();

            foreach (var method in methods)
            {
                if (!IsEntityLookupMethod(entityType, method))
                    continue;

                var invoker = TypeMemberInvoker.GetInvoker(method);

                if (resultBuilder.ContainsKey(invoker.FirstParameterType))
                {
                    // TODO: Throw?
                    continue;
                }

                async ValueTask<IEntityLoadResult> LookupAccessor(object handler, object message, IServiceProvider serviceProvider, CancellationToken cancellation)
                {
                    object ResolveParameter(ParameterInfo parameter)
                    {
                        if (parameter.ParameterType == typeof(IServiceProvider))
                        {
                            return serviceProvider;
                        }
                        else if (parameter.ParameterType == typeof(CancellationToken))
                        {
                            return cancellation;
                        }
                        else if (ParameterDefaultValue.TryGetDefaultValue(parameter, out var defaultValue))
                        {
                            return serviceProvider.GetService(parameter.ParameterType) ?? defaultValue;
                        }
                        else
                        {
                            return serviceProvider.GetRequiredService(parameter.ParameterType);
                        }
                    }

                    var result = await invoker.InvokeAsync(handler, message, ResolveParameter)
                        .ConfigureAwait(false);

                    if (result is IEntityLoadResult entityLoadResult)
                    {
                        return entityLoadResult;
                    }

                    return new NotFoundEntityQueryResult(default, false);

                    // TODO: We could load the metadata we need (id, concurrency-token, revision) 
                    //       with the entity-manager, if we allow the entity to be returned directly.
                }

                resultBuilder.Add(invoker.FirstParameterType, LookupAccessor);
            }

            return resultBuilder.ToImmutable();
        }

#pragma warning disable IDE0060, CA1801 // TODO: Allow the entity to be returned directly.
        private static bool IsEntityLookupMethod(Type entityType, MethodInfo method)
#pragma warning restore IDE0060, CA1801
        {
            var parameters = method.GetParameters();

            if (parameters.Any(p => p.ParameterType.IsByRef))
                return false;

            if (method.IsGenericMethod || method.IsGenericMethodDefinition)
                return false;

            if (!method.IsDefined<EntityLookupAttribute>())
                return false;

            var returnType = method.ReturnType;
            var typeDescriptor = AwaitableTypeDescriptor.GetTypeDescriptor(returnType);
            var resultType = typeDescriptor.ResultType;

            // TODO: Allow the entity to be returned directly.
            return typeof(IEntityLoadResult).IsAssignableFrom(resultType)
                /*|| entityType.IsAssignableFrom(resultType)*/;
        }

        #endregion

        #region Fields

        private readonly Type _handlerType;
        private readonly Action<object, object?> _entitySetter;
        private readonly Func<object, object?> _entityGetter;
        private readonly Func<object, bool>? _deleteFlagAccessor;

        private readonly ConcurrentDictionary<Type, LookupAccessor?> _matchingLookupAccessors;
        private readonly ImmutableDictionary<Type, LookupAccessor> _lookupAccessors; // message-type 2 lookup-accessor


        #endregion

        #region C'tor

        private EntityMessageHandlerContextDescriptor(Type handlerType, PropertyInfo entityProperty)
        {
            Debug.Assert(entityProperty.DeclaringType!.IsAssignableFrom(handlerType));

            BuildEntityAccessor(handlerType, entityProperty, out _entityGetter, out _entitySetter);

            EntityType = GetEntityType(entityProperty);
            _deleteFlagAccessor = BuildDeleteFlagAccessor(handlerType);
            _lookupAccessors = BuildLookupAccessors(handlerType, EntityType);

            _matchingLookupAccessors = new ConcurrentDictionary<Type, LookupAccessor?>();
            _handlerType = handlerType;
        }

        #endregion

        /// <summary>
        /// Gets the type of entity the message handler manages.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Sets the specified entity to the specified message handler.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <param name="entity">The entity.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if either <paramref name="handler"/> is not of type the descriptor was created for or a derived type
        /// or <paramref name="entity"/> is not of type <see cref="EntityType"/> or a derived type.
        /// </exception>
        public void SetHandlerEntity(object handler, object? entity)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            if (!_handlerType.IsAssignableFrom(handler.GetType()))
                throw new ArgumentException($"The argument must be of type '{_handlerType}' or a derived type.");

            if (entity != null && !EntityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The argument must be of type '{EntityType}' or a derived type.");

            _entitySetter(handler, entity);
        }

        /// <summary>
        /// Gets the entity from the specified message handler.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <returns>The entity extracted from <paramref name="handler"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="handler"/> is not of type the descriptor was created for or a derived type.
        /// </exception>
        public object? GetHandlerEntity(object handler)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            if (!_handlerType.IsAssignableFrom(handler.GetType()))
                throw new ArgumentException($"The argument must be of type '{_handlerType}' or a derived type.");

            return _entityGetter(handler);
        }

        /// <summary>
        /// Returns a boolean value indicating whether the entity of the specified message handler is marked as deleted.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <returns>
        /// True if the entity managed by <paramref name="handler"/> is marked as deleted, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="handler"/> is not of type the descriptor was created for or a derived type.
        /// </exception>
        public bool IsMarkedAsDeleted(object handler)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            if (!_handlerType.IsAssignableFrom(handler.GetType()))
                throw new ArgumentException($"The argument must be of type '{_handlerType}' or a derived type.");

            if (_deleteFlagAccessor is null)
                return false;

            return _deleteFlagAccessor(handler);
        }

        /// <summary>
        /// Tries to retrieve the custom lookup accessor for the specified message-type.
        /// </summary>
        /// <param name="messageType">The message-type.</param>
        /// <param name="lookupAccessor">Contains the lookup accessor if one can be retrieved.</param>
        /// <returns>
        /// True if a lookup accessor for <paramref name="messageType"/> can be retrieved, false otherwise.
        /// </returns>
        public bool TryGetLookupAccessor(Type messageType, [NotNullWhen(true)] out LookupAccessor? lookupAccessor)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (_matchingLookupAccessors is null)
            {
                lookupAccessor = null;
                return false;
            }

            if (!_matchingLookupAccessors.TryGetValue(messageType, out var result))
            {
                result = GetLookupAccessorCore(messageType);
                result = _matchingLookupAccessors.GetOrAdd(messageType, result);
            }

            lookupAccessor = result;
            return lookupAccessor != null;
        }

        private LookupAccessor? GetLookupAccessorCore(Type messageType)
        {
#pragma warning disable IDE0007
            for (Type? currentType = messageType; currentType != null; currentType = currentType.BaseType)
#pragma warning restore IDE0007
            {
                if (_lookupAccessors.TryGetValue(currentType, out var lookupAccessor))
                {
                    return lookupAccessor;
                }
            }

            if (_lookupAccessors.TryGetValue(typeof(void), out var noMessageLookupAccessor))
            {
                return noMessageLookupAccessor;
            }

            return null;
        }
    }

    /// <summary>
    /// A lookup accessor that performs an asynchronous custom entity lookup from the storage engine.
    /// </summary>
    /// <param name="handler">The message handler.</param>
    /// <param name="message">The handled message.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to lookup services.</param>
    /// <param name="cancellation">
    /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
    /// or <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{IEntityLoadResult}"/> representing the asynchronous operation.
    /// When evaluated, the tasks result contains the entity load-result.
    /// </returns>
    public delegate ValueTask<IEntityLoadResult> LookupAccessor(
        object handler,
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellation);
}
