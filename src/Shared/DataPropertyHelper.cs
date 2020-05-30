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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Utils;

#nullable enable

namespace AI4E.Internal
{
    internal static class DataPropertyHelper
    {
        private const BindingFlags DefaultBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        #region IdMember

        private static readonly ConcurrentDictionary<Type, MemberInfo?> _idMembers
            = new ConcurrentDictionary<Type, MemberInfo?>();

        // Cache delegate for perf reasons.
        private static readonly Func<Type, MemberInfo?> _getIdMemberUncached = GetIdMemberUncached;

        public static bool TryGetIdMemberCore(
            Type type,
            [NotNullWhen(true)] out MemberInfo? memberInfo)
        {
            memberInfo = _idMembers.GetOrAdd(type, _getIdMemberUncached);
            return memberInfo != null;
        }

        public static MemberInfo? GetIdMember<TData>()
        {
            if (!TryGetIdMemberCore(typeof(TData), out var memberInfo))
            {
                memberInfo = null;
            }

            return memberInfo;
        }

        public static bool TryGetIdMember<TData>([NotNullWhen(true)] out MemberInfo? memberInfo)
        {
            return TryGetIdMemberCore(typeof(TData), out memberInfo);
        }

        public static MemberInfo? GetIdMember(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (!TryGetIdMemberCore(type, out var memberInfo))
            {
                memberInfo = null;
            }

            return memberInfo;
        }

        public static bool TryGetIdMember(
            Type type,
            [NotNullWhen(true)] out MemberInfo? memberInfo)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return TryGetIdMemberCore(type, out memberInfo);
        }

        private static bool TryGetIdTypeCore(Type type, [NotNullWhen(true)]out Type? idType)
        {
            if (!TryGetIdMember(type, out var idMember))
            {
                idType = null;
                return false;
            }

            idType = idMember switch
            {
                MethodInfo method => method.ReturnType,
                FieldInfo field => field.FieldType,
                PropertyInfo property => property.PropertyType,
                _ => null,
            };

            return idType != null;
        }

        public static Type? GetIdType<TData>()
        {
            if (!TryGetIdTypeCore(typeof(TData), out var idType))
            {
                idType = null;
            }

            return idType;
        }

        public static Type? GetIdType(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (!TryGetIdTypeCore(type, out var idType))
            {
                idType = null;
            }

            return idType;
        }

        public static bool TryGetIdType<TData>([NotNullWhen(true)]out Type? idType)
        {
            return TryGetIdTypeCore(typeof(TData), out idType);
        }

        public static bool TryGetIdType(Type type, [NotNullWhen(true)]out Type? idType)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return TryGetIdTypeCore(type, out idType);
        }

        private static MemberInfo? GetIdMemberUncached(Type type)
        {
            if (!TryGetIdMemberUncached(type, out var result))
            {
                result = null;
            }

            return result;
        }

        private static bool TryGetIdMemberUncached(Type type, [MaybeNullWhen(false)] out MemberInfo member)
        {
            var properties = type.GetPublicProperties();

            // 1. A readable instance property called 'Id'
            var idProperty = properties.FirstOrDefault(p => p.Name == "Id" && p.CanRead);
            var getter = idProperty?.GetMethod;

            if (getter != null && !getter.IsStatic)
            {
                Debug.Assert(idProperty != null);
                member = idProperty!;
                return true;
            }

            // 2. A readable instance property called '{TypeName}' + Id
            idProperty = properties.FirstOrDefault(p => p.Name == type.Name + "Id" && p.CanRead);
            getter = idProperty?.GetMethod;

            if (getter != null && !getter.IsStatic)
            {
                Debug.Assert(idProperty != null);
                member = idProperty!;
                return true;
            }

            // 3. A non void-returning parameterless instance method called 'GetId'
            var idMethod = type.GetMethod(
                "GetId",
                DefaultBindingFlags,
                binder: default,
                Type.EmptyTypes,
                modifiers: default);

            if (idMethod != null && idMethod.ReturnType != typeof(void))
            {
                member = idMethod;
                return true;
            }

            // 4. An instance field called 'id'
            var idField = type.GetField(
                "id",
                DefaultBindingFlags);

            if (idField != null)
            {
                member = idField;
                return true;
            }

            // 5. An instance field called '_id'
            idField = type.GetField(
                "_id",
                DefaultBindingFlags);

            if (idField != null)
            {
                member = idField;
                return true;
            }

#pragma warning disable CA1308
            var lowercaseTypeName = type.Name.Substring(0, 1).ToLowerInvariant() + type.Name.Substring(1);
#pragma warning restore CA1308

            // 6. An instance field called '{LowerCaseTypeName}Id'
            idField = type.GetField(
                lowercaseTypeName + "Id",
                DefaultBindingFlags);

            if (idField != null)
            {
                member = idField;
                return true;
            }

            // 7. An instance field called '_{LowerCaseTypeName}id'
            idField = type.GetField("_" + lowercaseTypeName + "Id", DefaultBindingFlags);

            if (idField != null)
            {
                member = idField;
                return true;
            }

            member = null!;
            return false;
        }

        #endregion

        #region GetId

        private static readonly ConcurrentDictionary<Type, Delegate?> _idAccessors
            = new ConcurrentDictionary<Type, Delegate?>();

        // Cache delegate for perf reasons.
        private static readonly Func<Type, Delegate?> _getIdAccessor = GetIdAccessor;

        public static Func<TData, TId>? GetIdAccessor<TId, TData>()
        {
            var idType = GetIdType<TData>();

            if (idType is null)
            {
                return null;
            }

            if (!typeof(TId).IsAssignableFrom(idType))
            {
                throw new Exception("Type mismatch.");
            }

            return _idAccessors.GetOrAdd(typeof(TData), _getIdAccessor) as Func<TData, TId>;
        }

        [return: MaybeNull]
        public static TId GetId<TId, TData>(TData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var idType = GetIdType<TData>();

            if (idType is null)
            {
                return default!;
            }

            if (!typeof(TId).IsAssignableFrom(idType))
            {
                throw new InvalidOperationException($"The type {typeof(TData)} does not have an id member that is assignable to type {typeof(TId)}.");
            }

            if (_idAccessors.GetOrAdd(typeof(TData), _getIdAccessor) is Func<TData, TId> idAccessor)
            {
                return idAccessor.Invoke(data);
            }

            return default!;
        }

        public static object? GetId(Type dataType, object data)
        {
            if (!TryGetId(dataType, data, out var result))
            {
                result = null;
            }

            return result;
        }

        public static bool TryGetId(Type dataType, object data, [NotNullWhen(true)] out object? id)
        {
            if (dataType is null)
                throw new ArgumentNullException(nameof(dataType));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

            if (!dataType.IsAssignableFrom(data.GetType()))
                throw new ArgumentException();

            var idType = GetIdType(dataType);

            if (idType is null)
            {
                id = null;
                return false;
            }

            var idAccessor = _idAccessors.GetOrAdd(dataType, _getIdAccessor);

            if (idAccessor is null)
            {
                id = null;
                return false;
            }

            id = idAccessor.DynamicInvoke(data);
            return true;
        }

        private static Delegate? GetIdAccessor(Type type)
        {
            return BuildIdAccessorCore(type)?.Compile();
        }

        public static LambdaExpression? BuildIdAccessor(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return BuildIdAccessorCore(type);
        }

        public static LambdaExpression? BuildIdAccessorCore(Type type)
        {
            var param = Expression.Parameter(type);

            if (TryBuildIdAccess(type, param, out var idAccess, out var idType))
            {
                return Expression.Lambda(typeof(Func<,>).MakeGenericType(type, idType), idAccess, param);
            }

            return null;
        }

        private static bool TryBuildIdAccess(
            Type type,
            Expression instance,
            [NotNullWhen(true)] out Expression? idAccess,
            [NotNullWhen(true)] out Type? idType)
        {
            if (TryGetIdMember(type, out var idMember))
            {
                if (idMember is MethodInfo method)
                {
                    idType = method.ReturnType;
                    idAccess = Expression.Call(instance, method);
                    Debug.Assert(idAccess.Type == idType);
                    return true;
                }

                if (idMember is FieldInfo field)
                {
                    idType = field.FieldType;
                    idAccess = Expression.MakeMemberAccess(instance, field);
                    Debug.Assert(idAccess.Type == idType);
                    return true;
                }

                if (idMember is PropertyInfo property)
                {
                    idType = property.PropertyType;
                    idAccess = Expression.MakeMemberAccess(instance, property);
                    Debug.Assert(idAccess.Type == idType);
                    return true;
                }
            }

            idAccess = null;
            idType = null;
            return false;
        }

        #endregion

        public static Expression BuildIdEqualityExpression(Type idType, Expression leftOperand, Expression rightOperand)
        {
            if (idType is null)
                throw new ArgumentNullException(nameof(idType));

            if (leftOperand is null)
                throw new ArgumentNullException(nameof(leftOperand));

            if (rightOperand is null)
                throw new ArgumentNullException(nameof(rightOperand));

            var equalityOperator = idType.GetMethod("op_Equality", BindingFlags.Static); // TODO

            if (equalityOperator != null)
            {
                return Expression.Equal(leftOperand, rightOperand);
            }

            Expression areEqual;

            if (idType.GetInterfaces().Any(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IEquatable<>) && p.GetGenericArguments()[0] == idType))
            {
                var equalsMethod = typeof(IEquatable<>).MakeGenericType(idType).GetMethod(nameof(Equals));
                areEqual = Expression.Call(leftOperand, equalsMethod, rightOperand);
            }
            else
            {
                var equalsMethod = typeof(object).GetMethod(nameof(Equals), BindingFlags.Public | BindingFlags.Instance);
                areEqual = Expression.Call(Expression.Convert(leftOperand, typeof(object)), equalsMethod, Expression.Convert(rightOperand, typeof(object)));
            }

            return Expression.AndAlso(IsNotNull(idType, leftOperand), areEqual);
        }

        private static Expression IsNotNull(Type idType, Expression operand)
        {
            if (idType.IsConstructedGenericType && idType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var hasValueProperty = idType.GetProperty(
                    "HasValue",
                    BindingFlags.Public | BindingFlags.Instance,
                    Type.DefaultBinder,
                    returnType: typeof(bool),
                    Type.EmptyTypes,
                    modifiers: null);

                return Expression.MakeMemberAccess(operand, hasValueProperty);
            }

            if (!idType.IsValueType)
            {
                return Expression.ReferenceNotEqual(operand, Expression.Constant(null, idType));
            }

            return Expression.Constant(true);
        }

        public static Expression<Func<TId, TId, bool>> BuildIdEquality<TId>()
        {
            var idType = typeof(TId);
            var leftOperand = Expression.Parameter(idType);
            var rightOperand = Expression.Parameter(idType);

            return Expression.Lambda<Func<TId, TId, bool>>(BuildIdEqualityExpression(idType, leftOperand, rightOperand), leftOperand, rightOperand);
        }

        public static Expression<Func<TData, bool>>? BuildPredicate<TId, TData>(TId id)
        {
            var param = Expression.Parameter(typeof(TData));
            if (TryBuildIdAccess(typeof(TData), param, out var idAccess, out var idType))
            {
                var idConstant = Expression.Convert(Expression.Constant(id), idType);
                var equalityExpression = BuildIdEqualityExpression(idType, idConstant, idAccess);
                return Expression.Lambda<Func<TData, bool>>(equalityExpression, param);
            }

            return null;
        }

        public static Expression<Func<TData, bool>> BuildPredicate<TId, TData>(TId id, Expression<Func<TData, TId>> idAccessor)
        {
            if (idAccessor == null)
                throw new ArgumentNullException(nameof(idAccessor));

            var idConstant = Expression.Constant(id);
            var equalityExpression = BuildIdEqualityExpression(typeof(TId), idConstant, idAccessor.Body);
            return Expression.Lambda<Func<TData, bool>>(equalityExpression, idAccessor.Parameters.First());
        }

        public static Expression<Func<TData, bool>>? BuildPredicate<TData>(TData comparand)
        {
            var comparandConstant = Expression.Constant(comparand);
            var param = Expression.Parameter(typeof(TData));

            if (!TryBuildIdAccess(typeof(TData), comparandConstant, out var idAccessComparand, out var idType))
                return null;

            if (!TryBuildIdAccess(typeof(TData), param, out var idAccessParam, out _))
                return null;

            var equalityExpression = BuildIdEqualityExpression(idType, idAccessComparand, idAccessParam);
            return Expression.Lambda<Func<TData, bool>>(equalityExpression, param);
        }

        public static bool TryBuildPredication<TData>(
            TData comparand,
            [NotNullWhen(true)] out Expression<Func<TData, bool>>? predicate)
        {
            var comparandConstant = Expression.Constant(comparand);
            var param = Expression.Parameter(typeof(TData));

            if (!TryBuildIdAccess(typeof(TData), comparandConstant, out var idAccessComparand, out var idType))
            {
                predicate = null;
                return false;
            }

            if (!TryBuildIdAccess(typeof(TData), param, out var idAccessParam, out _))
            {
                predicate = null;
                return false;
            }

            var equalityExpression = BuildIdEqualityExpression(idType, idAccessComparand, idAccessParam);
            predicate = Expression.Lambda<Func<TData, bool>>(equalityExpression, param);
            return true;
        }

        public static LambdaExpression? BuildPredicate(Type dataType, object comparand)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (comparand == null)
                throw new ArgumentNullException(nameof(comparand));

            if (!dataType.IsAssignableFrom(comparand.GetType()))
            {
                throw new ArgumentException($"The specified object must be of type {dataType.FullName} or a derived type");
            }

            var comparandConstant = Expression.Constant(comparand);
            var param = Expression.Parameter(dataType);

            if (!TryBuildIdAccess(dataType, comparandConstant, out var idAccessComparand, out var idType))
                return null;

            if (!TryBuildIdAccess(dataType, param, out var idAccessParam, out _))
                return null;

            var equalityExpression = BuildIdEqualityExpression(idType, idAccessComparand, idAccessParam);
            return Expression.Lambda(equalityExpression, param);
        }

        public static Func<object, bool>? CompilePredicate(Type dataType, object comparand)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (comparand == null)
                throw new ArgumentNullException(nameof(comparand));

            var predicate = BuildPredicate(dataType, comparand);

            if (predicate is null)
                return null;

            var parameter = Expression.Parameter(typeof(object));
            var equality = ParameterExpressionReplacer.ReplaceParameter(predicate.Body, predicate.Parameters.First(), Expression.Convert(parameter, dataType));
            var typeCheck = Expression.TypeIs(parameter, dataType);

            var lambda = Expression.Lambda<Func<object, bool>>(Expression.AndAlso(typeCheck, equality), parameter);
            return lambda.Compile();
        }

        public static Func<TData, bool>? CompilePredicate<TData>(TData comparand)
        {
            return BuildPredicate(comparand)?.Compile();
        }
    }
}
