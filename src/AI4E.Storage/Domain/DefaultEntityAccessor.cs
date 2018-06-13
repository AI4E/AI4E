using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Domain
{
    public sealed class DefaultEntityAccessor : IEntityAccessor
    {
        private readonly ConcurrentDictionary<Type, TypedDefaultEntityAccessor> _typedAccessors;

        public DefaultEntityAccessor()
        {
            _typedAccessors = new ConcurrentDictionary<Type, TypedDefaultEntityAccessor>();
        }

        public void CommitEvents(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedDefaultEntityAccessor(entityType);
            typedAccessor.CommitEvents(entity);
        }

        public string GetConcurrencyToken(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedDefaultEntityAccessor(entityType);
            return typedAccessor.GetConcurrencyToken(entity);
        }

        public string GetId(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedDefaultEntityAccessor(entityType);
            return typedAccessor.GetId(entity);
        }

        public long GetRevision(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedDefaultEntityAccessor(entityType);
            return typedAccessor.GetRevision(entity);
        }

        public IEnumerable<object> GetUncommittedEvents(Type entityType, object entity)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedDefaultEntityAccessor(entityType);
            return typedAccessor.GetUncommittedEvents(entity);
        }

        public void SetConcurrencyToken(Type entityType, object entity, string concurrencyToken)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedDefaultEntityAccessor(entityType);
            typedAccessor.SetConcurrencyToken(entity, concurrencyToken);
        }

        public void SetRevision(Type entityType, object entity, long revision)
        {
            CheckArguments(entityType, entity);

            var typedAccessor = GetTypedDefaultEntityAccessor(entityType);
            typedAccessor.SetRevision(entity, revision);
        }

        private static void CheckArguments(Type entityType, object entity)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The argument must be of type '{entityType.FullName}' or a derived type.", nameof(entity));
        }

        private TypedDefaultEntityAccessor GetTypedDefaultEntityAccessor(Type entityType)
        {
            Assert(entityType != null);

            return _typedAccessors.GetOrAdd(entityType, _ => new TypedDefaultEntityAccessor(entityType));
        }

        private sealed class TypedDefaultEntityAccessor
        {
            private static readonly MethodInfo _toStringMethod = typeof(object).GetMethod(nameof(ToString));

            private readonly Func<object, string> _idAccessor;
            private readonly Func<object, string> _concurrencyTokenAccessor;
            private readonly Action<object, string> _concurrencyTokenSetter;
            private readonly Func<object, long> _revisionAccessor;
            private readonly Action<object, long> _revisionSetter;
            private readonly Func<object, IEnumerable<object>> _uncommittedEventsAccessor;
            private readonly Action<object> _commitMethodInvoker;

            public TypedDefaultEntityAccessor(Type entityBaseType)
            {
                var entityParameter = Expression.Parameter(typeof(object), "entity");
                var convertedParameter = Expression.Convert(entityParameter, entityBaseType);

                var concurrencyTokenProperty = entityBaseType.GetProperty("ConcurrencyToken",
                                                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                            binder: default,
                                                            typeof(string),
                                                            new Type[0],
                                                            new ParameterModifier[0]);

                if (concurrencyTokenProperty == null || !concurrencyTokenProperty.CanRead || !concurrencyTokenProperty.CanWrite)
                {
                    throw new Exception(); // TODO
                }

                var revisionProperty = entityBaseType.GetProperty("Revision",
                                                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                            binder: default,
                                                            typeof(long),
                                                            new Type[0],
                                                            new ParameterModifier[0]);

                if (revisionProperty == null || !revisionProperty.CanRead || !revisionProperty.CanWrite)
                {
                    throw new Exception(); // TODO
                }

                var uncommittedEventsProperty = entityBaseType.GetProperty("UncommittedEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (uncommittedEventsProperty == null ||
                    !uncommittedEventsProperty.CanRead ||
                    uncommittedEventsProperty.GetIndexParameters().Length != 0 ||
                    !typeof(IEnumerable<object>).IsAssignableFrom(uncommittedEventsProperty.PropertyType))
                {
                    throw new Exception(); // TODO
                }

                var commitEventsMethod = entityBaseType.GetMethod("CommitEvents",
                                                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                                    binder: default,
                                                                    new Type[0],
                                                                    new ParameterModifier[0]);

                if (commitEventsMethod == null)
                {
                    throw new Exception(); // TODO
                }

                _idAccessor = BuildIdAccessor(entityParameter, convertedParameter);

                var concurrencyTokenParameter = Expression.Parameter(typeof(string), "concurrencyToken");
                var concurrencyTokenPropertyAccess = Expression.Property(convertedParameter, concurrencyTokenProperty);
                var concurrencyTokenPropertyAssign = Expression.Assign(concurrencyTokenPropertyAccess, concurrencyTokenParameter);
                _concurrencyTokenAccessor = Expression.Lambda<Func<object, string>>(concurrencyTokenPropertyAccess, entityParameter).Compile();
                _concurrencyTokenSetter = Expression.Lambda<Action<object, string>>(concurrencyTokenPropertyAssign, entityParameter, concurrencyTokenParameter).Compile();

                var revisionParameter = Expression.Parameter(typeof(long), "revision");
                var revisionPropertyAccess = Expression.Property(convertedParameter, revisionProperty);
                var revisionPropertyAssign = Expression.Assign(revisionPropertyAccess, revisionParameter);
                _revisionAccessor = Expression.Lambda<Func<object, long>>(revisionPropertyAccess, entityParameter).Compile();
                _revisionSetter = Expression.Lambda<Action<object, long>>(revisionPropertyAssign, entityParameter, revisionParameter).Compile();

                var uncommittedEventsPropertyAccess = Expression.Property(convertedParameter, uncommittedEventsProperty);
                _uncommittedEventsAccessor = Expression.Lambda<Func<object, IEnumerable<object>>>(uncommittedEventsPropertyAccess, entityParameter).Compile();

                var commitMethodCall = Expression.Call(convertedParameter, commitEventsMethod);
                _commitMethodInvoker = Expression.Lambda<Action<object>>(commitMethodCall, entityParameter).Compile();
            }

            private static Func<object, string> BuildIdAccessor(ParameterExpression parameter, Expression entity)
            {
                var entityBaseType = entity.Type;

                var idProperty = entityBaseType.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (idProperty == null || !idProperty.CanRead || idProperty.GetIndexParameters().Length != 0)
                {
                    throw new Exception(); // TODO
                }

                return BuildIdAccessor(parameter, entity, idProperty);
            }

            private static Func<object, string> BuildIdAccessor(ParameterExpression parameter, Expression entity, PropertyInfo idProperty)
            {
                var idCall = Expression.MakeMemberAccess(entity, idProperty);
                Expression body;

                if (!idProperty.PropertyType.IsValueType)
                {
                    var isNotNull = Expression.ReferenceNotEqual(idCall, Expression.Constant(null, idProperty.PropertyType));
                    var conversion = Expression.Call(idCall, _toStringMethod);
                    body = Expression.Condition(isNotNull, conversion, Expression.Constant(null, typeof(string)));
                }
                else if (idProperty.PropertyType.IsGenericType &&
                         idProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var underlyingType = idProperty.PropertyType.GetGenericArguments().First();
                    var nullableType = typeof(Nullable<>).MakeGenericType(underlyingType);
                    var hasValueProperty = nullableType.GetProperty("HasValue");
                    var valueProperty = nullableType.GetProperty("Value");

                    var isNotNull = Expression.MakeMemberAccess(idCall, hasValueProperty);
                    var value = Expression.MakeMemberAccess(idCall, valueProperty);
                    var conversion = Expression.Call(value, _toStringMethod);
                    body = Expression.Condition(isNotNull, value, Expression.Constant(null, typeof(string)));
                }
                else
                {
                    body = Expression.Call(idCall, _toStringMethod);
                }

                var idLambda = Expression.Lambda<Func<object, string>>(body, parameter);
                return idLambda.Compile();
            }


            public string GetId(object entity)
            {
                return _idAccessor(entity);
            }

            public string GetConcurrencyToken(object entity)
            {
                return _concurrencyTokenAccessor(entity);
            }

            public void SetConcurrencyToken(object entity, string concurrencyToken)
            {
                _concurrencyTokenSetter(entity, concurrencyToken);
            }

            public long GetRevision(object entity)
            {
                return _revisionAccessor(entity);
            }

            public void SetRevision(object entity, long revision)
            {
                _revisionSetter(entity, revision);
            }

            public void CommitEvents(object entity)
            {
                _commitMethodInvoker(entity);
            }

            public IEnumerable<object> GetUncommittedEvents(object entity)
            {
                return _uncommittedEventsAccessor(entity);
            }
        }
    }

    public static class EntityAccessorExtension
    {
        public static void CommitEvents(this IEntityAccessor entityAccessor, object entity)
        {
            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entityAccessor.CommitEvents(entity.GetType(), entity);
        }

        public static string GetConcurrencyToken(this IEntityAccessor entityAccessor, object entity)
        {
            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return entityAccessor.GetConcurrencyToken(entity.GetType(), entity);
        }

        public static string GetId(this IEntityAccessor entityAccessor, object entity)
        {
            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return entityAccessor.GetId(entity.GetType(), entity);
        }

        public static long GetRevision(this IEntityAccessor entityAccessor, object entity)
        {
            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return entityAccessor.GetRevision(entity.GetType(), entity);
        }

        public static IEnumerable<object> GetUncommittedEvents(this IEntityAccessor entityAccessor, object entity)
        {
            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return entityAccessor.GetUncommittedEvents(entity.GetType(), entity);
        }

        public static void SetConcurrencyToken(this IEntityAccessor entityAccessor, object entity, string concurrencyToken)
        {
            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entityAccessor.SetConcurrencyToken(entity.GetType(), entity, concurrencyToken);
        }

        public static void SetRevision(this IEntityAccessor entityAccessor, object entity, long revision)
        {
            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entityAccessor.SetRevision(entity.GetType(), entity, revision);
        }
    }
}
