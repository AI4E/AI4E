/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        ParameterExpressionReplacer.cs 
 * Types:           (1) AI4E.ParameterExpressionReplacer
 *                  (2) AI4E.ParameterExpressionReplacer.ReplacerExpressionVisitor
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.06.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using System.Linq.Expressions;
using static System.Diagnostics.Debug;

namespace AI4E
{
    public static class ParameterExpressionReplacer
    {
        private static ObjectPool<ReplacerExpressionVisitor> _pool;

        static ParameterExpressionReplacer()
        {
            _pool = new ObjectPool<ReplacerExpressionVisitor>(() => new ReplacerExpressionVisitor());
        }

        public static Expression ReplaceParameter(Expression expression, ParameterExpression parameter, Expression replacement)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            ReplacerExpressionVisitor parameterExpressionReplacer = null;

            // TODO: Implement a RAII style object rental that can be used like the following:
            //       using(var rentedObject = _pool.RentObject()) { var parameterExpressionReplacer = rentedObject.Value; [...]}
            try
            {
                parameterExpressionReplacer = _pool.GetObject();
                parameterExpressionReplacer.SetExpressions(parameter, replacement);
                return parameterExpressionReplacer.Visit(expression);
            }
            finally
            {
                if (parameterExpressionReplacer != null)
                {
                    _pool.PutObject(parameterExpressionReplacer);
                }
            }
        }

        private sealed class ReplacerExpressionVisitor : ExpressionVisitor
        {
            private ParameterExpression _parameterExpression;
            private Expression _replacement;

            public void SetExpressions(ParameterExpression parameterExpression, Expression replacement)
            {
                Assert(parameterExpression != null);
                Assert(replacement != null);
                Assert(parameterExpression.Type.IsAssignableFrom(replacement.Type));

                _parameterExpression = parameterExpression;
                _replacement = replacement;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _parameterExpression)
                {
                    return _replacement;
                }

                return base.VisitParameter(node);
            }
        }
    }
}
