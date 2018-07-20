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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Storage;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    public sealed class EntityMessageHandlerProcessor : MessageProcessor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEntityStorageEngine _entityStorageEngine;
        private readonly IEntityIdManager _entityIdManager;
        private readonly IEntityStoragePropertyManager _entityStoragePropertyManager; // TODO: Rename
        private volatile IMessageAccessor _messageAccessor = null;

        private static readonly ConcurrentDictionary<Type, MessageHandlerDescriptor> _descriptor = new ConcurrentDictionary<Type, MessageHandlerDescriptor>();

        public EntityMessageHandlerProcessor(IServiceProvider serviceProvider,
                                             IEntityStorageEngine entityStorageEngine,
                                             IEntityIdManager entityIdManager,
                                             IEntityStoragePropertyManager entityStoragePropertyManager) // TODO: Rename
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (entityStorageEngine == null)
                throw new ArgumentNullException(nameof(entityStorageEngine));

            if (entityIdManager == null)
                throw new ArgumentNullException(nameof(entityIdManager));

            if (entityStoragePropertyManager == null)
                throw new ArgumentNullException(nameof(entityStoragePropertyManager));

            _serviceProvider = serviceProvider;
            _entityStorageEngine = entityStorageEngine;
            _entityIdManager = entityIdManager;
            _entityStoragePropertyManager = entityStoragePropertyManager;
        }

        public async override Task<IDispatchResult> ProcessAsync<TMessage>(TMessage message,
                                                                           Func<TMessage, Task<IDispatchResult>> next)
        {
            var handler = Context.MessageHandler;
            var descriptor = _descriptor.GetOrAdd(handler.GetType(), handlerType => new MessageHandlerDescriptor(handlerType));

            if (!descriptor.IsEntityMessageHandler)
            {
                return await next(message);
            }

            if (!TryGetEntityLookup(message, descriptor, out var entityLookup))
            {
                return await next(message);
            }

            var messageAccessor = GetMessageAccessor();
            var checkConcurrencyToken = messageAccessor.TryGetConcurrencyToken(message, out var concurrencyToken);

            do
            {
                var entity = await entityLookup();
                var createsEntityAttribute = Context.MessageHandlerAction.Member.GetCustomAttribute<CreatesEntityAttribute>();

                if (entity == null)
                {
                    if (createsEntityAttribute == null ||
                        !createsEntityAttribute.CreatesEntity)
                    {
                        return new EntityNotFoundDispatchResult(descriptor.EntityType);
                    }
                }
                else
                {
                    if (createsEntityAttribute != null &&
                        createsEntityAttribute.CreatesEntity &&
                        !createsEntityAttribute.AllowExisingEntity)
                    {
                        if (!_entityIdManager.TryGetId(descriptor.EntityType, entity, out var id))
                        {
                            return new EntityAlreadyPresentDispatchResult(descriptor.EntityType);
                        }
                        else
                        {
                            return new EntityAlreadyPresentDispatchResult(descriptor.EntityType, id);
                        }
                    }

                    if (checkConcurrencyToken &&
                        concurrencyToken != _entityStoragePropertyManager.GetConcurrencyToken(entity))
                    {
                        return new ConcurrencyIssueDispatchResult();
                    }

                    descriptor.SetHandlerEntity(handler, entity);
                }

                var originalEntity = entity;
                var dispatchResult = await next(message);

                if (!dispatchResult.IsSuccess)
                {
                    return dispatchResult;
                }

                var markedAsDeleted = descriptor.IsMarkedAsDeleted(handler);
                entity = descriptor.GetHandlerEntity(handler);

                try
                {
                    // The Store/Delete calls must be protected to be called with a null entity.
                    if (entity == null && originalEntity == null)
                    {
                        // TODO: Do we care about events etc. here?
                        return dispatchResult;
                    }

                    if (!_entityIdManager.TryGetId(descriptor.EntityType, entity ?? originalEntity, out var id))
                    {
                        return new FailureDispatchResult("Unable to determine the id of the specified entity.");
                    }

                    if (markedAsDeleted || entity == null)
                    {
                        await _entityStorageEngine.DeleteAsync(descriptor.EntityType, entity ?? originalEntity, id);
                    }
                    else
                    {
                        await _entityStorageEngine.StoreAsync(descriptor.EntityType, entity, id);
                    }
                }
                catch (ConcurrencyException)
                {
                    continue;
                }
                catch (StorageException exc)
                {
                    return new StorageIssueDispatchResult(exc);
                }
                catch (Exception exc)
                {
                    return new FailureDispatchResult(exc);
                }

                return dispatchResult;

            }
            while (!checkConcurrencyToken);

            return new ConcurrencyIssueDispatchResult();
        }

        private bool TryGetEntityLookup<TMessage>(TMessage message, MessageHandlerDescriptor descriptor, out Func<ValueTask<object>> entityLookup)
        {
            var lookupAccessor = GetMatchingLookupAccessor<TMessage>(descriptor);

            if (lookupAccessor != null)
            {
                entityLookup = () => new ValueTask<object>(lookupAccessor(Context.MessageHandler, message));
                return true;
            }

            var messageAccessor = GetMessageAccessor();

            if (messageAccessor.TryGetEntityId(message, out var id))
            {
                entityLookup = () => _entityStorageEngine.GetByIdAsync(descriptor.EntityType, id);
                return true;
            }

            entityLookup = default;
            return false;
        }

        private static Func<object, object, Task<object>> GetMatchingLookupAccessor<TMessage>(in MessageHandlerDescriptor descriptor)
        {
            Func<object, object, Task<object>> noMessageAccessor = null;

            foreach (var (messageType, accessor) in descriptor.LookupAccessors)
            {
                if (messageType == null)
                {
                    noMessageAccessor = accessor;
                    continue;
                }

                if (messageType.IsAssignableFrom(typeof(TMessage)))
                {
                    return accessor;
                }
            }

            return noMessageAccessor;
        }

        private IMessageAccessor GetMessageAccessor()
        {
            if (_messageAccessor != null)
            {
                return _messageAccessor;
            }

            var messageAccessor = _serviceProvider.GetService<IMessageAccessor>();

            if (messageAccessor == null)
            {
                messageAccessor = new DefaultMessageAccessor();
            }

            return Interlocked.CompareExchange(ref _messageAccessor, messageAccessor, null) ?? messageAccessor;
        }

        private readonly struct MessageHandlerDescriptor
        {
            private readonly Action<object, object> _entitySetter;
            private readonly Func<object, object> _entityGetter;
            private readonly Func<object, bool> _deleteFlagAccessor;

            public MessageHandlerDescriptor(Type handlerType)
            {
                var entityProperty = GetEntityProperty(handlerType);

                if (entityProperty == null)
                {
                    this = default;
                    return;
                }

                BuildEntityAccessor(handlerType, entityProperty, out _entityGetter, out _entitySetter);

                EntityType = GetEntityType(entityProperty);

                _deleteFlagAccessor = BuildDeleteFlagAccessor(handlerType);
                LookupAccessors = BuildLookupAccessors(handlerType, EntityType).ToImmutableArray();
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
                var handlerConvert = Expression.Convert(handlerParam, handlerType);
                var entityConvert = Expression.Convert(entityParam, entityProperty.PropertyType);
                var propertyAccess = Expression.Property(handlerConvert, entityProperty);
                var propertyAssign = Expression.Assign(propertyAccess, entityConvert);
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
                var handlerConvert = Expression.Convert(handlerParam, handlerType);
                var deleteFlagPropertyAccess = Expression.Property(handlerConvert, deleteFlagProperty);
                var deleteFlagLambda = Expression.Lambda<Func<object, bool>>(deleteFlagPropertyAccess, handlerParam);

                return deleteFlagLambda.Compile();
            }

            private static IEnumerable<(Type messageType, Func<object, object, Task<object>> lookupAccessor)> BuildLookupAccessors(Type handlerType, Type entityType)
            {
                var methods = GetEntityLookupMethods(handlerType, entityType);

                foreach (var method in methods)
                {
                    var messageType = method.GetParameters().FirstOrDefault()?.ParameterType;
                    var isAsync = typeof(Task).IsAssignableFrom(method.ReturnType);
                    var handlerParam = Expression.Parameter(typeof(object), "handler");
                    var messageParam = Expression.Parameter(typeof(object), "message");
                    var convertedHandler = Expression.Convert(handlerParam, handlerType);

                    Expression call;

                    if (messageType == null)
                    {
                        call = Expression.Call(convertedHandler, method);
                    }
                    else
                    {
                        var convertedMessage = Expression.Convert(messageParam, messageType);
                        call = Expression.Call(convertedHandler, method, convertedMessage);
                    }

                    var accessor = Expression.Lambda<Func<object, object, object>>(call, handlerParam, messageParam).Compile();

                    if (!isAsync)
                    {
                        yield return (messageType, (handler, message) => EvaluateSync(handler, message, accessor));
                    }
                    else
                    {
                        yield return (messageType, (handler, message) => EvaluateAsync(handler, message, accessor, method.ReturnType));
                    }
                }
            }

            private static Task<object> EvaluateSync(object handler, object message, Func<object, object, object> accessor)
            {
                var result = accessor(handler, message);
                return Task.FromResult(result);
            }

            private static async Task<object> EvaluateAsync(object handler, object message, Func<object, object, object> accessor, Type returnType)
            {
                var result = (Task)accessor(handler, message);
                await result;

                var taskParameter = Expression.Parameter(typeof(Task), "task");
                var taskParameterConversion = Expression.Convert(taskParameter, returnType);
                var resultAccess = Expression.Property(taskParameterConversion, returnType.GetProperty("Result"));

                var evaluator = Expression.Lambda<Func<Task, object>>(resultAccess, taskParameter).Compile();

                return evaluator(result);
            }

            private static IEnumerable<MethodInfo> GetEntityLookupMethods(Type handlerType, Type entityType)
            {
                return handlerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                  .Where(p => IsEntityLookupMethod(entityType, p));
            }

            private static bool IsEntityLookupMethod(Type entityType, MethodInfo method)
            {
                if (method.IsGenericMethodDefinition)
                    return false;

                var parameters = method.GetParameters();

                if (parameters.Length > 1)
                    return false;

                if (!method.IsDefined<EntityLookupAttribute>())
                    return false;

                var returnType = method.ReturnType;

                if (entityType.IsAssignableFrom(returnType))
                    return true;

                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    returnType = returnType.GetGenericArguments().First();

                    if (entityType.IsAssignableFrom(returnType))
                        return true;
                }

                return false;
            }

            public bool IsEntityMessageHandler => _entityGetter != null && _entitySetter != null;
            public Type EntityType { get; }

            public ImmutableArray<(Type messageType, Func<object, object, Task<object>> lookupAccessor)> LookupAccessors { get; }

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
        }
    }
}
