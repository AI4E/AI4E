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
using System.Linq.Expressions;
using Microsoft.Extensions.ObjectPool;
using static System.Diagnostics.Debug;

namespace AI4E.Utils
{
    public sealed class ParameterExpressionReplacer
    {
        private static readonly ObjectPool<ReplacerExpressionVisitor> _pool
            = new DefaultObjectPool<ReplacerExpressionVisitor>(
                new DefaultPooledObjectPolicy<ReplacerExpressionVisitor>());

        public static Expression ReplaceParameter(
            Expression expression, ParameterExpression parameter, Expression replacement)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            using (_pool.Get(out var replaceExpressionVisitor))
            {
                replaceExpressionVisitor.SetExpressions(parameter, replacement);
                return replaceExpressionVisitor.Visit(expression);
            }
        }

        private sealed class ReplacerExpressionVisitor : ExpressionVisitor
        {
            private ParameterExpression? _parameterExpression;
            private Expression? _replacement;

            public void SetExpressions(ParameterExpression parameterExpression, Expression replacement)
            {
                Assert(parameterExpression.Type.IsAssignableFrom(replacement.Type));

                _parameterExpression = parameterExpression;
                _replacement = replacement;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _parameterExpression)
                {
                    return _replacement!;
                }

                return base.VisitParameter(node);
            }
        }
    }
}
