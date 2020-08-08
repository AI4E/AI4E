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

using System.Reflection;
using System.Threading;

namespace System.Linq.Expressions
{
    /// <summary>
    /// Contains extensions for the <see cref="Expression"/> type.
    /// </summary>
    public static class AI4EUtilsExpressionExtension
    {
        /// <summary>
        /// Tries to evaluates the specified expression and returns the result if successful.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="result">Contains the result of the expression evaluation if the operation succeeds.</param>
        /// <returns>True if the expression was evaluated successfully, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is <c>null</c>.</exception>
        public static bool TryEvaluate(this Expression expression, out object? result)
        {
            if (expression is ConstantExpression constant)
            {
                result = constant.Value;
                return true;
            }

            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Member is FieldInfo field &&
                    memberExpression.Expression is ConstantExpression fieldOwner)
                {
                    result = field.GetValue(fieldOwner.Value);
                    return true;
                }
            }

            if (ParameterExpressionFinder.ContainsParameters(expression))
            {
                result = null;
                return false;
            }

            var valueFactory = Expression.Lambda<Func<object?>>(
                Expression.Convert(expression, typeof(object))).Compile(preferInterpretation: true);

            result = valueFactory();
            return true;
        }

        /// <summary>
        /// Evaluates the specified expression and returns the result.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <returns>The result of the expression evaluation.</returns>
        public static object? Evaluate(this Expression expression)
        {
            if (!TryEvaluate(expression, out var result))
            {
                result = null;
            }

            return result;
        }

        private sealed class ParameterExpressionFinder : ExpressionVisitor
        {
            private bool _containsParameters;

            private static readonly ThreadLocal<ParameterExpressionFinder> _instances
                = new ThreadLocal<ParameterExpressionFinder>(
                    () => new ParameterExpressionFinder(),
                    trackAllValues: false);

            public static bool ContainsParameters(Expression expression)
            {
                var visitor = _instances.Value!;
                visitor._containsParameters = false;
                visitor.Visit(expression);
                return visitor._containsParameters;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                _containsParameters = true;
                return base.VisitParameter(node);
            }
        }

        private static readonly MethodInfo _toStringMethod = typeof(object).GetMethod(nameof(ToString))!;

        /// <summary>
        /// Converts the specified expression to a string expression by calling the <see cref="object.ToString"/> 
        /// method.
        /// </summary>
        /// <param name="expression">The expression to convert.</param>
        /// <returns>The converted expression.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="expression"/> is <c>null</c>.</exception>
        public static Expression ToStringExpression(this Expression expression)
        {
            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            var expressionType = expression.Type;

            if (!expressionType.IsValueType)
            {
                var isNotNull = Expression.ReferenceNotEqual(expression, Expression.Constant(null, expressionType));
                var convertedExpression = Expression.Call(expression, _toStringMethod);
                return Expression.Condition(isNotNull, convertedExpression, Expression.Constant(null, typeof(string)));
            }

            if (expressionType.IsGenericType && expressionType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = expressionType.GetGenericArguments().First();
                var nullableType = typeof(Nullable<>).MakeGenericType(underlyingType);
                var hasValueProperty = nullableType.GetProperty("HasValue");
                var valueProperty = nullableType.GetProperty("Value");

                var isNotNull = Expression.MakeMemberAccess(expression, hasValueProperty);
                var value = Expression.MakeMemberAccess(expression, valueProperty);
                var convertedValue = Expression.Call(value, _toStringMethod);
                return Expression.Condition(isNotNull, convertedValue, Expression.Constant(null, typeof(string)));
            }

            return Expression.Call(expression, _toStringMethod);
        }
    }
}
