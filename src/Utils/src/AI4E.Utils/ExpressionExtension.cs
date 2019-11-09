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

using System.Reflection;

namespace System.Linq.Expressions
{
    /// <summary>
    /// Contains extensions for the <see cref="Expression"/> type.
    /// </summary>
    public static class AI4EUtilsExpressionExtension
    {
        /// <summary>
        /// Evaluates the specified expression and returns the result.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <returns>The result of the expression evaluation.</returns>
        public static object? Evaluate(this Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Member is FieldInfo field &&
                    memberExpression.Expression is ConstantExpression fieldOwner)
                {
                    return field.GetValue(fieldOwner.Value);
                }
            }

            var valueFactory = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile();

            return valueFactory();
        }
    }
}
