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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;

namespace AI4E.Storage.Domain.Projection
{
    internal sealed class ProjectionResultComparer : IEqualityComparer<IProjectionResult>
    {
        private readonly ConcurrentDictionary<Type, Func<object, object, bool>> _idComparer;

        public static ProjectionResultComparer Instance { get; } = new ProjectionResultComparer();

        private ProjectionResultComparer()
        {
            _idComparer = new ConcurrentDictionary<Type, Func<object, object, bool>>();
        }

        public bool Equals(IProjectionResult x, IProjectionResult y)
        {
            return x.ResultType == y.ResultType &&
#if DEBUG
                   // We do not actually have to check this, as this condition is included in the result type check.
                   x.ResultIdType == y.ResultIdType &&
#endif
                   GetIdComparer(x.ResultIdType).Invoke(x, y);
        }

        public int GetHashCode(IProjectionResult obj)
        {
            return (obj.ResultType, obj.ResultId).GetHashCode();
        }

        private Func<object, object, bool> GetIdComparer(Type idType)
        {
            return _idComparer.GetOrAdd(idType, BuildIdComparer);
        }

        private static Func<object, object, bool> BuildIdComparer(Type idType)
        {
            var left = Expression.Parameter(typeof(object), "left");
            var right = Expression.Parameter(typeof(object), "right");
            var convertedLeft = Expression.Convert(left, idType);
            var convertedRight = Expression.Convert(right, idType);
            var comparison = BuildIdEqualityExpression(idType, convertedLeft, convertedRight);
            var lambda = Expression.Lambda<Func<object, object, bool>>(comparison, left, right);
            return lambda.Compile();
        }

        // TODO: Copied from DataPropertyHelper
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
    }
}
