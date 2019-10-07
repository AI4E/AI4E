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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Utils;
using static System.Diagnostics.Debug;

#nullable disable

namespace AI4E.Internal
{
    internal static class DataPropertyHelper
    {
        private static readonly ConcurrentDictionary<Type, MemberInfo> _idMembers = new ConcurrentDictionary<Type, MemberInfo>();
        private static readonly ConcurrentDictionary<Type, Delegate> _idAccessors = new ConcurrentDictionary<Type, Delegate>();

        public static MemberInfo GetIdMember<TData>()
        {
            return _idMembers.GetOrAdd(typeof(TData), type => GetIdMemberInternal(type));
        }

        public static MemberInfo GetIdMember(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _idMembers.GetOrAdd(type, _ => GetIdMemberInternal(type));
        }

        private static MemberInfo GetIdMemberInternal(Type type)
        {
            var properties = type.GetPublicProperties();

            // 1. A readable instance property called 'Id'
            var idProperty = properties.FirstOrDefault(p => p.Name == "Id" && p.CanRead);
            var getter = idProperty?.GetMethod;

            if (getter != null && !getter.IsStatic)
            {
                return idProperty;
            }

            // 2. A readable instance property called '{TypeName}' + Id
            idProperty = properties.FirstOrDefault(p => p.Name == type.Name + "Id" && p.CanRead);
            getter = idProperty?.GetMethod;

            if (getter != null && !getter.IsStatic)
            {
                return idProperty;
            }

            // 3. A non void-returning parameterless instance method called 'GetId'
            var idMethod = type.GetMethod("GetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: default, Type.EmptyTypes, modifiers: default);

            if (idMethod != null && idMethod.ReturnType != typeof(void))
            {
                return idMethod;
            }

            // 4. An instance field called 'id'
            var idField = type.GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (idField != null)
            {
                return idProperty;
            }

            // 5. An instance field called '_id'
            idField = type.GetField("_id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (idField != null)
            {
                return idProperty;
            }

            var lowercaseTypeName = type.Name.Substring(0, 1).ToLowerInvariant() + type.Name.Substring(1);

            // 6. An instance field called '{LowerCaseTypeName}Id'
            idField = type.GetField(lowercaseTypeName + "Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (idField != null)
            {
                return idProperty;
            }

            // 7. An instance field called '_{LowerCaseTypeName}id'
            idField = type.GetField("_" + lowercaseTypeName + "Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (idField != null)
            {
                return idProperty;
            }

            return null;
        }

        public static Type GetIdType<TData>()
        {
            return GetIdType(typeof(TData));
        }

        public static Type GetIdType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var idMember = GetIdMember(type);

            if (idMember != null)
            {
                if (idMember.MemberType == MemberTypes.Method)
                    return ((MethodInfo)idMember).ReturnType;

                if (idMember.MemberType == MemberTypes.Field)
                    return ((FieldInfo)idMember).FieldType;

                if (idMember.MemberType == MemberTypes.Property)
                    return ((PropertyInfo)idMember).PropertyType;
            }

            return null;
        }

        public static Func<TData, TId> GetIdAccessor<TId, TData>()
        {
            var idType = GetIdType<TData>();

            if (!typeof(TId).IsAssignableFrom(idType))
            {
                throw new Exception("Type mismatch.");
            }

            var idAccessor = (Func<TData, TId>)_idAccessors.GetOrAdd(typeof(TData), GetIdAccessor);

            Assert(idAccessor != null);

            return idAccessor;
        }

        public static TId GetId<TId, TData>(TData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var idType = GetIdType<TData>();

            if (idType == null || !typeof(TId).IsAssignableFrom(idType))
            {
                throw new InvalidOperationException($"The type {typeof(TData)} does not have an id member that is assignable to type {typeof(TId)}.");
            }

            var idAccessor = (Func<TData, TId>)_idAccessors.GetOrAdd(typeof(TData), GetIdAccessor);

            Assert(idAccessor != null);

            return idAccessor(data);
        }

        public static object GetId(Type dataType, object data)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (!dataType.IsAssignableFrom(data.GetType()))
                throw new ArgumentException();

            var idType = GetIdType(dataType);
            var idAccessor = _idAccessors.GetOrAdd(dataType, _ => GetIdAccessor(dataType));

            Assert(idAccessor != null);

            return idAccessor.DynamicInvoke(data);
        }

        public static LambdaExpression BuildIdAccessor(Type type)
        {
            var idMember = GetIdMember(type);
            var idType = GetIdType(type);

            if (idMember == null)
                return null;

            var param = Expression.Parameter(type);
            Expression idAccess;

            if (idMember.MemberType == MemberTypes.Method)
            {
                idAccess = Expression.Call(param, (MethodInfo)idMember);
            }
            else if (idMember.MemberType == MemberTypes.Field || idMember.MemberType == MemberTypes.Property)
            {
                idAccess = Expression.MakeMemberAccess(param, idMember);
            }
            else
            {
                return null;
            }

            return Expression.Lambda(typeof(Func<,>).MakeGenericType(type, idType), idAccess, param);
        }

        private static Delegate GetIdAccessor(Type type)
        {
            return BuildIdAccessor(type).Compile();
        }

        private static Expression BuildIdEqualityExpression(Type idType, Expression leftOperand, Expression rightOperand)
        {
            var equalityOperator = idType.GetMethod("op_Equality", BindingFlags.Static); // TODO

            if (equalityOperator != null)
            {
                return Expression.Equal(leftOperand, rightOperand);
            }
            else if (idType.GetInterfaces().Any(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IEquatable<>) && p.GetGenericArguments()[0] == idType))
            {
                var equalsMethod = typeof(IEquatable<>).MakeGenericType(idType).GetMethod(nameof(Equals));

                // TODO: Check left operand to be non-null
                return Expression.Call(leftOperand, equalsMethod, rightOperand);
            }
            else
            {
                var equalsMethod = typeof(object).GetMethod(nameof(Equals), BindingFlags.Public | BindingFlags.Instance);

                // TODO: Check left operand to be non-null
                return Expression.Call(Expression.Convert(leftOperand, typeof(object)), equalsMethod, Expression.Convert(rightOperand, typeof(object)));
            }
        }

        public static Expression<Func<TId, TId, bool>> BuildIdEquality<TId>()
        {
            var idType = typeof(TId);
            var leftOperand = Expression.Parameter(idType);
            var rightOperand = Expression.Parameter(idType);

            return Expression.Lambda<Func<TId, TId, bool>>(BuildIdEqualityExpression(idType, leftOperand, rightOperand), leftOperand, rightOperand);
        }

        public static Expression<Func<TData, bool>> BuildPredicate<TId, TData>(TId id)
        {
            var idMember = GetIdMember<TData>();

            if (idMember == null)
            {
                throw new /*Storage*/Exception($"Unable to resolve primary key for type '{typeof(TData).FullName}'");
            }

            var idType = GetIdType<TData>();
            var param = Expression.Parameter(typeof(TData));
            var idConstant = Expression.Constant(id);
            Expression idAccessParam;

            if (idMember.MemberType == MemberTypes.Method)
            {
                idAccessParam = Expression.Call(param, (MethodInfo)idMember);
            }
            else if (idMember.MemberType == MemberTypes.Field || idMember.MemberType == MemberTypes.Property)
            {
                idAccessParam = Expression.MakeMemberAccess(param, idMember);
            }
            else
            {
                return null;
            }

            var equalityExpression = BuildIdEqualityExpression(idType, idConstant, idAccessParam);
            return Expression.Lambda<Func<TData, bool>>(equalityExpression, param);
        }

        public static Expression<Func<TData, bool>> BuildPredicate<TId, TData>(TId id, Expression<Func<TData, TId>> idAccessor)
        {
            if (idAccessor == null)
                throw new ArgumentNullException(nameof(idAccessor));

            var idConstant = Expression.Constant(id);

            var equalityExpression = BuildIdEqualityExpression(typeof(TId), idConstant, idAccessor.Body);
            return Expression.Lambda<Func<TData, bool>>(equalityExpression, idAccessor.Parameters.First());
        }

        public static Expression<Func<TData, bool>> BuildPredicate<TData>(TData comparand)
        {
            var idMember = GetIdMember<TData>();

            if (idMember == null)
            {
                throw new /*Storage*/Exception($"Unable to resolve primary key for type '{typeof(TData).FullName}'");
            }

            var idType = GetIdType<TData>();
            var param = Expression.Parameter(typeof(TData));
            var comparandConstant = Expression.Constant(comparand);
            Expression idAccessComparand, idAccessParam;

            if (idMember.MemberType == MemberTypes.Method)
            {
                idAccessComparand = Expression.Call(comparandConstant, (MethodInfo)idMember);
                idAccessParam = Expression.Call(param, (MethodInfo)idMember);
            }
            else if (idMember.MemberType == MemberTypes.Field || idMember.MemberType == MemberTypes.Property)
            {
                idAccessComparand = Expression.MakeMemberAccess(comparandConstant, idMember);
                idAccessParam = Expression.MakeMemberAccess(param, idMember);
            }
            else
            {
                return null;
            }

            var equalityExpression = BuildIdEqualityExpression(idType, idAccessComparand, idAccessParam);
            return Expression.Lambda<Func<TData, bool>>(equalityExpression, param);
        }

        public static LambdaExpression BuildPredicate(Type dataType, object comparand)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (comparand == null)
                throw new ArgumentNullException(nameof(comparand));

            if (!dataType.IsAssignableFrom(comparand.GetType()))
            {
                throw new ArgumentException($"The specified object must be of type {dataType.FullName} or a derived type");
            }

            var idMember = GetIdMember(dataType);

            if (idMember == null)
            {
                throw new /*Storage*/Exception($"Unable to resolve primary key for type '{dataType.FullName}'");
            }

            var idType = GetIdType(dataType);
            var param = Expression.Parameter(dataType);
            var comparandConstant = Expression.Constant(comparand, dataType);
            Expression idAccessComparand, idAccessParam;

            if (idMember.MemberType == MemberTypes.Method)
            {
                idAccessComparand = Expression.Call(comparandConstant, (MethodInfo)idMember);
                idAccessParam = Expression.Call(param, (MethodInfo)idMember);
            }
            else if (idMember.MemberType == MemberTypes.Field || idMember.MemberType == MemberTypes.Property)
            {
                idAccessComparand = Expression.MakeMemberAccess(comparandConstant, idMember);
                idAccessParam = Expression.MakeMemberAccess(param, idMember);
            }
            else
            {
                return null;
            }

            var equalityExpression = BuildIdEqualityExpression(idType, idAccessComparand, idAccessParam);
            return Expression.Lambda(equalityExpression, param);
        }

        public static Func<object, bool> CompilePredicate(Type dataType, object comparand)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (comparand == null)
                throw new ArgumentNullException(nameof(comparand));

            var predicate = BuildPredicate(dataType, comparand);

            var parameter = Expression.Parameter(typeof(object));
            var equality = ParameterExpressionReplacer.ReplaceParameter(predicate.Body, predicate.Parameters.First(), Expression.Convert(parameter, dataType));
            var typeCheck = Expression.TypeIs(parameter, dataType);

            var lambda = Expression.Lambda<Func<object, bool>>(Expression.AndAlso(typeCheck, equality), parameter);
            return lambda.Compile();
        }

        public static Func<TData, bool> CompilePredicate<TData>(TData comparand)
        {
            return BuildPredicate(comparand).Compile();
        }

        // TODO: This has some duplications in EntityPropertyAccessor
        #region Revision

        internal static PropertyInfo GetRevisionProperty(Type entityType)
        {
            var result = entityType.GetProperty("Revision",
                                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (result == null)
                return null;

            if (result.GetIndexParameters().Length != 0)
            {
                return null;
            }

            if (result.PropertyType != typeof(long))
            {
                return null;
            }

            if (!result.CanRead)
            {
                return null;
            }

            return result;
        }

        internal static long GetRevision(Type entityType, object entity)
        {
            var revisionProperty = GetRevisionProperty(entityType);

            if (revisionProperty == null)
            {
                throw new NotSupportedException();
            }

            var entityParameter = Expression.Parameter(typeof(object), "entity");
            var convertedEntity = Expression.Convert(entityParameter, entityType);
            var revisionPropertyAccess = Expression.Property(convertedEntity, revisionProperty);

            return Expression.Lambda<Func<object, long>>(revisionPropertyAccess, entityParameter).Compile()(entity);
        }

        #endregion
    }
}
