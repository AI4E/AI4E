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
using System.Linq;
using AI4E.DispatchResults;
using AI4E.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E
{
    [TestClass]
    public class DispatchResultExtensionTests
    {
        #region NotAuthorized

        [TestMethod]
        public void NotAuthorizedTest()
        {
            var dispatchResult = (IDispatchResult)new NotAuthorizedDispatchResult();
            Assert.IsTrue(dispatchResult.IsNotAuthorized());
        }

        [TestMethod]
        public void NotAuthorized2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsNotAuthorized());
        }

        [TestMethod]
        public void NotAuthorized3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new NotAuthorizedDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsNotAuthorized());
        }

        [TestMethod]
        public void NotAuthorized4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsNotAuthorized());
        }

        #endregion

        #region NotAuthenticated

        [TestMethod]
        public void NotAuthenticatedTest()
        {
            var dispatchResult = (IDispatchResult)new NotAuthenticatedDispatchResult();
            Assert.IsTrue(dispatchResult.IsNotAuthenticated());
        }

        [TestMethod]
        public void NotAuthenticated2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsNotAuthenticated());
        }

        [TestMethod]
        public void NotAuthenticated3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new NotAuthenticatedDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsNotAuthenticated());
        }

        [TestMethod]
        public void NotAuthenticated4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsNotAuthenticated());
        }

        #endregion

        #region ValidationFailed

        [TestMethod]
        public void ValidationFailedTest()
        {
            var validationResultsBuilder = new ValidationResultsBuilder();
            validationResultsBuilder.AddValidationResult("a", "b");
            validationResultsBuilder.AddValidationResult("c", "d");
            var validationResults = validationResultsBuilder.GetValidationResults();

            var dispatchResult = (IDispatchResult)new ValidationFailureDispatchResult(validationResults);
            Assert.IsTrue(dispatchResult.IsValidationFailed());
        }

        [TestMethod]
        public void ValidationFailed2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsValidationFailed());
        }

        [TestMethod]
        public void ValidationFailed3Test()
        {
            var validationResultsBuilder = new ValidationResultsBuilder();
            validationResultsBuilder.AddValidationResult("a", "b");
            validationResultsBuilder.AddValidationResult("c", "d");
            var validationResults = validationResultsBuilder.GetValidationResults();

            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new ValidationFailureDispatchResult(validationResults);
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsValidationFailed());
        }

        [TestMethod]
        public void ValidationFailed4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsValidationFailed());
        }

        [TestMethod]
        public void ValidationFailed5Test()
        {
            var validationResultsBuilder = new ValidationResultsBuilder();
            validationResultsBuilder.AddValidationResult("a", "b");
            validationResultsBuilder.AddValidationResult("c", "d");
            var validationResults = validationResultsBuilder.GetValidationResults();

            var dispatchResult = (IDispatchResult)new ValidationFailureDispatchResult(validationResults);
            Assert.IsTrue(dispatchResult.IsValidationFailed(out var validationResults1));
            Assert.AreEqual(validationResults.Count(), validationResults1.Count());
            Assert.IsTrue(validationResults.ToHashSet().SetEquals(validationResults1));

        }

        [TestMethod]
        public void ValidationFailed6Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsValidationFailed(out var validationResults1));
            Assert.AreEqual(0, validationResults1.Count());
        }

        [TestMethod]
        public void ValidationFailed7Test()
        {
            var validationResultsBuilder = new ValidationResultsBuilder();
            validationResultsBuilder.AddValidationResult("a", "b");
            validationResultsBuilder.AddValidationResult("c", "d");
            var validationResults = validationResultsBuilder.GetValidationResults();

            var validationResultsBuilder2 = new ValidationResultsBuilder();
            validationResultsBuilder.AddValidationResult("x", "y");
            var validationResults2 = validationResultsBuilder2.GetValidationResults();

            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new ValidationFailureDispatchResult(validationResults);
            var dispatchResult3 = new ValidationFailureDispatchResult(validationResults2);
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1, dispatchResult3 });

            Assert.IsTrue(aggregateDispatchResult2.IsValidationFailed(out var validationResults1));
            Assert.AreEqual(validationResults.Count() + validationResults2.Count(), validationResults1.Count());
            Assert.IsTrue(validationResults.Concat(validationResults2).ToHashSet().SetEquals(validationResults1));
        }

        [TestMethod]
        public void ValidationFailed8Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsValidationFailed(out var validationResults1));
            Assert.AreEqual(0, validationResults1.Count());
        }

        #endregion

        #region ConcurrencyIssue

        [TestMethod]
        public void ConcurrencyIssueTest()
        {
            var dispatchResult = (IDispatchResult)new ConcurrencyIssueDispatchResult();
            Assert.IsTrue(dispatchResult.IsConcurrencyIssue());
        }

        [TestMethod]
        public void ConcurrencyIssue2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsConcurrencyIssue());
        }

        [TestMethod]
        public void ConcurrencyIssue3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new ConcurrencyIssueDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsConcurrencyIssue());
        }

        [TestMethod]
        public void ConcurrencyIssue4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsConcurrencyIssue());
        }

        #endregion

        #region EntityNotFound

        [TestMethod]
        public void EntityNotFoundTest()
        {
            var dispatchResult = (IDispatchResult)new EntityNotFoundDispatchResult();
            Assert.IsTrue(dispatchResult.IsEntityNotFound());
        }

        [TestMethod]
        public void EntityNotFound2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsEntityNotFound());
        }

        [TestMethod]
        public void EntityNotFound3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new EntityNotFoundDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsEntityNotFound());
        }

        [TestMethod]
        public void EntityNotFound4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsEntityNotFound());
        }

        [TestMethod]
        public void EntityNotFound5Test()
        {
            var type = typeof(CustomMessage);
            var id = "abc";

            var dispatchResult = (IDispatchResult)new EntityNotFoundDispatchResult(type, id);
            Assert.IsTrue(dispatchResult.IsEntityNotFound(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.AreEqual(id, id1);
        }

        [TestMethod]
        public void EntityNotFound6Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsEntityNotFound(out var type1, out var id1));
            Assert.IsNull(type1);
            Assert.IsNull(id1);
        }

        [TestMethod]
        public void EntityNotFound7Test()
        {
            var type = typeof(CustomMessage);
            var id = "abc";

            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new EntityNotFoundDispatchResult(type, id);
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsEntityNotFound(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.AreEqual(id, id1);
        }

        [TestMethod]
        public void EntityNotFound8Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsEntityNotFound(out var type1, out var id1));
            Assert.IsNull(type1);
            Assert.IsNull(id1);
        }

        [TestMethod]
        public void EntityNotFound9Test()
        {
            var type = typeof(CustomMessage);

            var dispatchResult = (IDispatchResult)new EntityNotFoundDispatchResult(type);
            Assert.IsTrue(dispatchResult.IsEntityNotFound(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.IsNull(id1);
        }

        [TestMethod]
        public void EntityNotFound10Test()
        {
            var type = typeof(CustomMessage);

            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new EntityNotFoundDispatchResult(type);
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsEntityNotFound(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.IsNull(id1);
        }

        #endregion

        #region EntityAlreadyPresent

        [TestMethod]
        public void EntityAlreadyPresentTest()
        {
            var dispatchResult = (IDispatchResult)new EntityAlreadyPresentDispatchResult();
            Assert.IsTrue(dispatchResult.IsEntityAlreadyPresent());
        }

        [TestMethod]
        public void EntityAlreadyPresent2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsEntityAlreadyPresent());
        }

        [TestMethod]
        public void EntityAlreadyPresent3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new EntityAlreadyPresentDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsEntityAlreadyPresent());
        }

        [TestMethod]
        public void EntityAlreadyPresent4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsEntityAlreadyPresent());
        }

        [TestMethod]
        public void EntityAlreadyPresent5Test()
        {
            var type = typeof(CustomMessage);
            var id = "abc";

            var dispatchResult = (IDispatchResult)new EntityAlreadyPresentDispatchResult(type, id);
            Assert.IsTrue(dispatchResult.IsEntityAlreadyPresent(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.AreEqual(id, id1);
        }

        [TestMethod]
        public void EntityAlreadyPresent6Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsEntityAlreadyPresent(out var type1, out var id1));
            Assert.IsNull(type1);
            Assert.IsNull(id1);
        }

        [TestMethod]
        public void EntityAlreadyPresent7Test()
        {
            var type = typeof(CustomMessage);
            var id = "abc";

            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new EntityAlreadyPresentDispatchResult(type, id);
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsEntityAlreadyPresent(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.AreEqual(id, id1);
        }

        [TestMethod]
        public void EntityAlreadyPresent8Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsEntityAlreadyPresent(out var type1, out var id1));
            Assert.IsNull(type1);
            Assert.IsNull(id1);
        }

        [TestMethod]
        public void EntityAlreadyPresent9Test()
        {
            var type = typeof(CustomMessage);

            var dispatchResult = (IDispatchResult)new EntityAlreadyPresentDispatchResult(type);
            Assert.IsTrue(dispatchResult.IsEntityAlreadyPresent(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.IsNull(id1);
        }

        [TestMethod]
        public void EntityAlreadyPresent10Test()
        {
            var type = typeof(CustomMessage);

            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new EntityAlreadyPresentDispatchResult(type);
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsEntityAlreadyPresent(out var type1, out var id1));
            Assert.AreEqual(type, type1);
            Assert.IsNull(id1);
        }

        #endregion

        #region Timeout

        [TestMethod]
        public void TimeoutTest()
        {
            var dispatchResult = (IDispatchResult)new TimeoutDispatchResult();
            Assert.IsTrue(dispatchResult.IsTimeout());
        }

        [TestMethod]
        public void Timeout2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsTimeout());
        }

        [TestMethod]
        public void Timeout3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new TimeoutDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsTimeout());
        }

        [TestMethod]
        public void Timeout4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsTimeout());
        }

        [TestMethod]
        public void Timeout5Test()
        {
            var dueTime = DateTime.UtcNow;
            var dispatchResult = (IDispatchResult)new TimeoutDispatchResult(dueTime);
            Assert.IsTrue(dispatchResult.IsTimeout(out var dueTime1));
            Assert.AreEqual(dueTime, dueTime1);
        }

        [TestMethod]
        public void Timeout6Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsTimeout(out var dueTime1));
            Assert.IsNull(dueTime1);
        }

        [TestMethod]
        public void Timeout7Test()
        {
            var dueTime = DateTime.UtcNow;
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new TimeoutDispatchResult(dueTime);
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsTimeout(out var dueTime1));
            Assert.AreEqual(dueTime, dueTime1);
        }

        [TestMethod]
        public void Timeout8Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsTimeout(out var dueTime1));
            Assert.IsNull(dueTime1);
        }

        [TestMethod]
        public void Timeout9Test()
        {
            var dispatchResult = (IDispatchResult)new TimeoutDispatchResult();
            Assert.IsTrue(dispatchResult.IsTimeout(out var dueTime1));
            Assert.IsNull(dueTime1);
        }

        [TestMethod]
        public void Timeout10Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new TimeoutDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsTimeout(out var dueTime1));
            Assert.IsNull(dueTime1);
        }

        #endregion

        #region AggregateResult

        [TestMethod]
        public void AggregateResultTest()
        {
            var dispatchResult = (IDispatchResult)new AggregateDispatchResult(Enumerable.Empty<IDispatchResult>());
            Assert.IsTrue(dispatchResult.IsAggregateResult());
        }

        [TestMethod]
        public void AggregateResult2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsAggregateResult());
        }

        [TestMethod]
        public void AggregateResult3Test()
        {
            var dispatchResult = (IDispatchResult)new AggregateDispatchResult(Enumerable.Empty<IDispatchResult>());
            Assert.IsTrue(dispatchResult.IsAggregateResult(out var aggregateDispatchResult));
            Assert.AreSame(dispatchResult, aggregateDispatchResult);
        }

        [TestMethod]
        public void AggregateResult4Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsAggregateResult(out var aggregateDispatchResult));
            Assert.IsNull(aggregateDispatchResult);
        }

        [TestMethod]
        public void FlattenTest()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new ValidationFailureDispatchResult();
            var dispatchResult3 = new ValidationFailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1, dispatchResult3 });

            var flattened = aggregateDispatchResult2.Flatten();
            Assert.AreEqual(3, flattened.DispatchResults.Count());
            Assert.IsTrue(new IDispatchResult[] { dispatchResult1, dispatchResult2, dispatchResult3 }.ToHashSet().SetEquals(flattened.DispatchResults));
        }

        [TestMethod]
        public void Flatten2Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new ValidationFailureDispatchResult();
            var dispatchResult3 = new ValidationFailureDispatchResult();
            var aggregateDispatchResult = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, dispatchResult2, dispatchResult3 });

            var flattened = aggregateDispatchResult.Flatten();

            Assert.AreSame(flattened, aggregateDispatchResult);
        }

        #endregion

        #region NotFound

        [TestMethod]
        public void NotFoundTest()
        {
            var dispatchResult = (IDispatchResult)new NotFoundDispatchResult();
            Assert.IsTrue(dispatchResult.IsNotFound());
        }

        [TestMethod]
        public void NotFound2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsNotFound());
        }

        [TestMethod]
        public void NotFound3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new NotFoundDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsNotFound());
        }

        [TestMethod]
        public void NotFound4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsNotFound());
        }

        #endregion

        #region Success

        [TestMethod]
        public void SuccessTest()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsTrue(dispatchResult.IsSuccess(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void Success2Test()
        {
            var result = 55;
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult<int>(result);
            Assert.IsTrue(dispatchResult.IsSuccess(out var result1));
            Assert.AreEqual(result, result1);
        }

        [TestMethod]
        public void Success3Test()
        {
            var dispatchResult = (IDispatchResult)new FailureDispatchResult();
            Assert.IsFalse(dispatchResult.IsSuccess(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void Success4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsSuccess(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void Success5Test()
        {
            var result = 55;
            var dispatchResult1 = new SuccessDispatchResult<int>(result);
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsSuccess(out var result1));
            Assert.AreEqual(result, result1);
        }

        [TestMethod]
        public void Success6Test()
        {
            var dispatchResult1 = new TimeoutDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsSuccess(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void Success7Test()
        {
            var result = new CustomMessage();
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult<CustomMessage>(result);
            Assert.IsTrue(dispatchResult.IsSuccess(out var result1));
            Assert.AreSame(result, result1);
        }

        [TestMethod]
        public void Success8Test()
        {
            var result = new CustomMessage();
            var dispatchResult1 = new SuccessDispatchResult<CustomMessage>(result);
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsSuccess(out var result1));
            Assert.AreSame(result, result1);
        }

        #endregion

        #region SuccessWithResult

        [TestMethod]
        public void SuccessWithResultTest()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsSuccessWithResult(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void SuccessWithResult2Test()
        {
            var result = 55;
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult<int>(result);
            Assert.IsTrue(dispatchResult.IsSuccessWithResult(out var result1));
            Assert.AreEqual(result, result1);
        }

        [TestMethod]
        public void SuccessWithResult3Test()
        {
            var dispatchResult = (IDispatchResult)new FailureDispatchResult();
            Assert.IsFalse(dispatchResult.IsSuccessWithResult(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void SuccessWithResult4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsSuccessWithResult(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void SuccessWithResult5Test()
        {
            var result = 55;
            var dispatchResult1 = new SuccessDispatchResult<int>(result);
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsSuccessWithResult(out var result1));
            Assert.AreEqual(result, result1);
        }

        [TestMethod]
        public void SuccessWithResult6Test()
        {
            var dispatchResult1 = new TimeoutDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsSuccessWithResult(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void SuccessWithResult7Test()
        {
            var result = new CustomMessage();
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult<CustomMessage>(result);
            Assert.IsTrue(dispatchResult.IsSuccessWithResult(out var result1));
            Assert.AreSame(result, result1);
        }

        [TestMethod]
        public void SuccessWithResult8Test()
        {
            var result = new CustomMessage();
            var dispatchResult1 = new SuccessDispatchResult<CustomMessage>(result);
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsSuccessWithResult(out var result1));
            Assert.AreSame(result, result1);
        }

        [TestMethod]
        public void SuccessWithResult9Test()
        {
            var result = new CustomMessage();
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult<CustomMessage>(result);
            Assert.IsFalse(dispatchResult.IsSuccessWithResult<string>(out var result1));
            Assert.IsNull(result1);
        }

        [TestMethod]
        public void SuccessWithResultATest()
        {
            var result = new CustomMessage();
            var dispatchResult1 = new SuccessDispatchResult<CustomMessage>(result);
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsSuccessWithResult<string>(out var result1));
            Assert.IsNull(result1);
        }

        #endregion

        #region DispatchFailure

        [TestMethod]
        public void DispatchFailureTest()
        {
            var dispatchResult = (IDispatchResult)new DispatchFailureDispatchResult(typeof(CustomMessage));
            Assert.IsTrue(dispatchResult.IsDispatchFailure());
        }

        [TestMethod]
        public void DispatchFailure2Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsDispatchFailure());
        }

        [TestMethod]
        public void DispatchFailure3Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new DispatchFailureDispatchResult(typeof(CustomMessage));
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsDispatchFailure());
        }

        [TestMethod]
        public void DispatchFailure4Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsDispatchFailure());
        }

        [TestMethod]
        public void DispatchFailure5Test()
        {
            var dispatchResult = (IDispatchResult)new DispatchFailureDispatchResult(typeof(CustomMessage));
            Assert.IsTrue(dispatchResult.IsDispatchFailure(out var type));
            Assert.AreEqual(typeof(CustomMessage), type);
        }

        [TestMethod]
        public void DispatchFailure6Test()
        {
            var dispatchResult = (IDispatchResult)new SuccessDispatchResult();
            Assert.IsFalse(dispatchResult.IsDispatchFailure(out var type));
            Assert.IsNull(type);
        }

        [TestMethod]
        public void DispatchFailure7Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new DispatchFailureDispatchResult(typeof(CustomMessage));
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsTrue(aggregateDispatchResult2.IsDispatchFailure(out var type));
            Assert.AreEqual(typeof(CustomMessage), type);
        }

        [TestMethod]
        public void DispatchFailure8Test()
        {
            var dispatchResult1 = new SuccessDispatchResult();
            var dispatchResult2 = new FailureDispatchResult();
            var aggregateDispatchResult1 = new AggregateDispatchResult(dispatchResult2.Yield());
            var aggregateDispatchResult2 = new AggregateDispatchResult(new IDispatchResult[] { dispatchResult1, aggregateDispatchResult1 });

            Assert.IsFalse(aggregateDispatchResult2.IsDispatchFailure(out var type));
            Assert.IsNull(type);
        }

        #endregion

        private sealed class CustomMessage { public string Id { get; set; } }
    }
}
