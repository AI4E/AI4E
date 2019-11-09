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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    public readonly struct EntityMessageHandlerContextDescriptor
    {
        #region Lookup, factory

        private static readonly ConcurrentDictionary<Type, EntityMessageHandlerContextDescriptor> _descriptors
           = new ConcurrentDictionary<Type, EntityMessageHandlerContextDescriptor>();

        public static EntityMessageHandlerContextDescriptor GetDescriptor(Type handlerType)
        {
            if (handlerType == null)
                throw new ArgumentNullException(nameof(handlerType));

            return _descriptors.GetOrAdd(handlerType, BuildDescriptor);
        }

        private static EntityMessageHandlerContextDescriptor BuildDescriptor(Type handlerType)
        {
            Assert(handlerType != null);

            var entityProperty = GetEntityProperty(handlerType);

            if (entityProperty == null)
            {
                return default;
            }

            BuildEntityAccessor(handlerType, entityProperty, out var entityGetter, out var entitySetter);

            var entityType = GetEntityType(entityProperty);

            var deleteFlagAccessor = BuildDeleteFlagAccessor(handlerType);
            var lookupAccessors = BuildLookupAccessors(handlerType, entityType).ToImmutableArray();

            return new EntityMessageHandlerContextDescriptor(entitySetter, entityGetter, deleteFlagAccessor, entityType, lookupAccessors);
        }

        private static PropertyInfo GetEntityProperty(Type handlerType)
        {
            return handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                            .FirstOrDefault(p => !p.PropertyType.IsValueType &&
                                                                  p.CanRead &&
                                                                  p.CanWrite &&
                                                                  p.GetIndexParameters().Length == 0 &&
                                                                  p.IsDefined<MessageHandlerEntityAttribute>());
        }

        private static void BuildEntityAccessor(Type handlerType,
                                                PropertyInfo entityProperty,
                                                out Func<object, object> entityGetter,
                                                out Action<object, object> entitySetter)
        {
            Assert(handlerType != null);
            Assert(entityProperty != null);

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var entityParam = Expression.Parameter(typeof(object), "entity");
            var convertedHandler = Expression.Convert(handlerParam, handlerType);
            var convertedEntity = Expression.Convert(entityParam, entityProperty.PropertyType);
            var propertyAccess = Expression.Property(convertedHandler, entityProperty);
            var propertyAssign = Expression.Assign(propertyAccess, convertedEntity);
            var getterLambda = Expression.Lambda<Func<object, object>>(propertyAccess, handlerParam);
            var setterLambda = Expression.Lambda<Action<object, object>>(propertyAssign, handlerParam, entityParam);

            entityGetter = getterLambda.Compile();
            entitySetter = setterLambda.Compile();
        }

        private static Type GetEntityType(PropertyInfo entityProperty)
        {
            Assert(entityProperty != null);

            var result = entityProperty.PropertyType;
            var customType = entityProperty.GetCustomAttribute<MessageHandlerEntityAttribute>().EntityType;

            if (customType != null &&
                result.IsAssignableFrom(customType)) // If the types do not match, we just ignore the custom type.
            {
                result = customType;
            }

            return result;
        }

        private static PropertyInfo GetDeleteFlagProperty(Type handlerType)
        {
            Assert(handlerType != null);

            return handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              .SingleOrDefault(p => p.PropertyType == typeof(bool) &&
                                                    p.CanRead &&
                                                    p.GetIndexParameters().Length == 0 &&
                                                    p.IsDefined<MessageHandlerEntityDeleteFlagAttribute>());
        }

        private static Func<object, bool> BuildDeleteFlagAccessor(Type handlerType)
        {
            Assert(handlerType != null);

            var deleteFlagProperty = GetDeleteFlagProperty(handlerType);

            if (deleteFlagProperty == null)
                return null;

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var convertedHandler = Expression.Convert(handlerParam, handlerType);
            var deleteFlagPropertyAccess = Expression.Property(convertedHandler, deleteFlagProperty);
            var deleteFlagLambda = Expression.Lambda<Func<object, bool>>(deleteFlagPropertyAccess, handlerParam);

            return deleteFlagLambda.Compile();
        }

        private static IEnumerable<(Type messageType, Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>> lookupAccessor)> BuildLookupAccessors(Type handlerType, Type entityType)
        {
            var methods = GetEntityLookupMethods(handlerType, entityType);

            foreach (var method in methods)
            {
                var invoker = TypeMemberInvoker.GetInvoker(method);

                ValueTask<object> LookupAccessor(object handler, object message, IServiceProvider serviceProvider, CancellationToken cancellation)
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
                        else if (parameter.HasDefaultValue)
                        {
                            return serviceProvider.GetService(parameter.ParameterType) ?? parameter.DefaultValue;
                        }
                        else
                        {
                            return serviceProvider.GetRequiredService(parameter.ParameterType);
                        }
                    }

                    return invoker.InvokeAsync(handler, message, ResolveParameter);
                }

                yield return (invoker.FirstParameterType, LookupAccessor);
            }
        }

        private static IEnumerable<MethodInfo> GetEntityLookupMethods(Type handlerType, Type entityType)
        {
            return handlerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              .Where(p => IsEntityLookupMethod(entityType, p));
        }

        private static bool IsEntityLookupMethod(Type entityType, MethodInfo member)
        {
            var parameters = member.GetParameters();

            if (/*parameters.Length == 0 ||*/ parameters.Any(p => p.ParameterType.IsByRef))
                return false;

            if (member.IsGenericMethod || member.IsGenericMethodDefinition)
                return false;

            if (!member.IsDefined<EntityLookupAttribute>())
                return false;

            var returnType = member.ReturnType;
            var typeDescriptor = AwaitableTypeDescriptor.GetTypeDescriptor(returnType);

            return entityType.IsAssignableFrom(typeDescriptor.ResultType);
        }

        #endregion

        private readonly ConcurrentDictionary<Type, Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>>> _matchingLookupAccessor;
        private readonly Action<object, object> _entitySetter;
        private readonly Func<object, object> _entityGetter;
        private readonly Func<object, bool> _deleteFlagAccessor;
        private readonly ImmutableArray<(Type messageType, Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>> lookupAccessor)> _lookupAccessors;

        private EntityMessageHandlerContextDescriptor(
            Action<object, object> entitySetter,
            Func<object, object> entityGetter,
            Func<object, bool> deleteFlagAccessor,
            Type entityType,
            ImmutableArray<(Type messageType, Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>> lookupAccessor)> lookupAccessors)
        {
            _matchingLookupAccessor = new ConcurrentDictionary<Type, Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>>>();
            _entitySetter = entitySetter;
            _entityGetter = entityGetter;
            _deleteFlagAccessor = deleteFlagAccessor;
            EntityType = entityType;
            _lookupAccessors = lookupAccessors;
        }

        public bool IsEntityMessageHandler => _entityGetter != null && _entitySetter != null;
        public Type EntityType { get; }

        public void SetHandlerEntity(object handler, object entity)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_entitySetter == null)
                throw new InvalidOperationException();

            _entitySetter(handler, entity);
        }

        public object GetHandlerEntity(object handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_entityGetter == null)
                throw new InvalidOperationException();

            return _entityGetter(handler);
        }

        public bool IsMarkedAsDeleted(object handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_deleteFlagAccessor == null)
                return false;

            return _deleteFlagAccessor(handler);
        }

        public Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>> GetLookupAccessor(Type messageType)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            // TODO: This allocates a delegate for each call.
            return _matchingLookupAccessor.GetOrAdd(messageType, GetLookupAccessorInternal);
        }

        private Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>> GetLookupAccessorInternal(Type messageType)
        {
            Func<object, object, IServiceProvider, CancellationToken, ValueTask<object>> fallbackMessageAccessor = null;

            // TODO: Select best matching accessor
            foreach (var (type, accessor) in _lookupAccessors)
            {
                if (type.IsAssignableFrom(messageType))
                {
                    return accessor;
                }

                if(type == typeof(void))
                {
                    fallbackMessageAccessor = accessor;
                }
            }

            return fallbackMessageAccessor;
        }
    }
}
