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

using System.Linq.Expressions;
using AI4E.Utils.TestTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils
{
    [TestClass]
    public class ExpressionExtensionTests
    {
        [TestMethod]
        public void EvaluateConstantExpressionTest()
        {
            var constantExpression = Expression.Constant(15, typeof(int));
            var value = constantExpression.Evaluate();

            Assert.AreEqual(15, value);
        }

        [TestMethod]
        public void EvaluateFieldOfConstantExpressionTest()
        {
            var testInstance = new ExpressionTestClass { _stringField = "123" };
            var instanceExpression = Expression.Constant(testInstance, typeof(ExpressionTestClass));
            var expression = Expression.MakeMemberAccess(instanceExpression, typeof(ExpressionTestClass).GetField(nameof(testInstance._stringField)));
            var value = expression.Evaluate();

            Assert.AreEqual("123", value);
        }

        [TestMethod]
        public void EvaluateComplexExpressionTest()
        {
            var testInstance = new ExpressionTestClass { _stringField = "123" };
            var instanceExpression = Expression.Constant(testInstance, typeof(ExpressionTestClass));
            var callExpression = Expression.Call(instanceExpression, typeof(ExpressionTestClass).GetMethod("GetStringValue"));
            var value = callExpression.Evaluate();

            Assert.AreEqual("123xyz", value);
        }
    }
}
