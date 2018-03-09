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
using System.Collections.Generic;
using AI4E.DispatchResults;

namespace AI4E
{
    [MessageHandler]
    public abstract class MessageHandler
    {
        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; internal set; }

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; internal set; }

        [NoAction]
        public virtual FailureDispatchResult Failure()
        {
            return new FailureDispatchResult();
        }

        [NoAction]
        public virtual FailureDispatchResult Failure(string message)
        {
            return new FailureDispatchResult(message);
        }

        [NoAction]
        public virtual FailureDispatchResult Failure(string message, IDispatchResult underlyingResult) // TODO
        {
            return new FailureDispatchResult(message);
        }

        [NoAction]
        public virtual SuccessDispatchResult Success()
        {
            return new SuccessDispatchResult();
        }

        [NoAction]
        public virtual SuccessDispatchResult Success(string message)
        {
            return new SuccessDispatchResult(message);
        }

        [NoAction]
        public virtual SuccessDispatchResult<TResult> Success<TResult>(TResult result)
        {
            return new SuccessDispatchResult<TResult>(result);
        }

        [NoAction]
        public virtual SuccessDispatchResult<TResult> Success<TResult>(TResult result, string message)
        {
            return new SuccessDispatchResult<TResult>(result, message);
        }

        [NoAction]
        public virtual ConcurrencyIssueDispatchResult ConcurrencyIssue()
        {
            return new ConcurrencyIssueDispatchResult();
        }

        [NoAction]
        public virtual EntityNotFoundDispatchResult EntityNotFound(Type entityType, string id)
        {
            return new EntityNotFoundDispatchResult(entityType, id);
        }

        [NoAction]
        public virtual EntityNotFoundDispatchResult EntityNotFound<TEntity>(string id)
        {
            return new EntityNotFoundDispatchResult(typeof(TEntity), id);
        }

        [NoAction]
        public virtual EntityNotFoundDispatchResult EntityNotFound<TEntity>(object id)
        {
            return new EntityNotFoundDispatchResult(typeof(TEntity), id.ToString());
        }

        [NoAction]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent(Type entityType, string id)
        {
            return new EntityAlreadyPresentDispatchResult(entityType, id);
        }

        [NoAction]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent<TEntity>(string id)
        {
            return new EntityAlreadyPresentDispatchResult(typeof(TEntity), id);
        }

        [NoAction]
        public virtual EntityAlreadyPresentDispatchResult EntityAlreadyPresent<TEntity>(object id)
        {
            return new EntityAlreadyPresentDispatchResult(typeof(TEntity), id.ToString());
        }

        [NoAction]
        public virtual NotAuthenticatedDispatchResult NotAuthenticated()
        {
            return new NotAuthenticatedDispatchResult();
        }

        [NoAction]
        public virtual NotAuthorizedDispatchResult NotAuthorized()
        {
            return new NotAuthorizedDispatchResult();
        }

        [NoAction]
        public virtual ValidationFailureDispatchResult ValidationFailure()
        {
            return new ValidationFailureDispatchResult();
        }

        [NoAction]
        public virtual ValidationFailureDispatchResult ValidationFailure(IEnumerable<ValidationResult> validationResults)
        {
            return new ValidationFailureDispatchResult(validationResults);
        }

        [NoAction]
        public virtual ValidationFailureDispatchResult ValidationFailure(params ValidationResult[] validationResults)
        {
            return new ValidationFailureDispatchResult(validationResults);
        }

        [NoAction]
        public virtual ValidationFailureDispatchResult ValidationFailure(string member, string message)
        {
            return new ValidationFailureDispatchResult(new[] { new ValidationResult(member, message) });
        }

        [NoAction]
        public virtual NotFoundDispatchResult NotFound()
        {
            return new NotFoundDispatchResult();
        }

        [NoAction]
        public virtual NotFoundDispatchResult NotFound(string message)
        {
            return new NotFoundDispatchResult(message);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class MessageHandlerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class MessageDispatcherAttribute : Attribute { }
}
